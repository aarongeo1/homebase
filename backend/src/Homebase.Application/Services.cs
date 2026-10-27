using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Homebase.Devices;
using Homebase.Domain;
using Homebase.Domain.Automations;
using Homebase.Domain.Devices;
using Homebase.Domain.Sun;

namespace Homebase.Application
{
    /// <summary>Everything the API needs, wired once (Api.Host and tests both build this).</summary>
    public sealed class HomeServices
    {
        public HomeServices(HomeRepositories repositories, DriverRegistry drivers, IEnumerable<IDeviceDiscovery> discoveries, IHouseholdClock clock, GeoLocation location)
        {
            Runtime = new HomeRuntime(repositories, drivers, clock, location);
            Devices = new DeviceService(Runtime, discoveries);
            Scenes = new SceneService(Runtime);
            Automations = new AutomationService(Runtime);
            Presence = new PresenceService(Runtime);
        }

        public HomeRuntime Runtime { get; }

        public DeviceService Devices { get; }

        public SceneService Scenes { get; }

        public AutomationService Automations { get; }

        public PresenceService Presence { get; }
    }

    public sealed class DiscoveryOutcome
    {
        public IReadOnlyList<Device> Added { get; set; }

        public IReadOnlyList<Device> Updated { get; set; }

        public IReadOnlyList<string> Notes { get; set; }
    }

    public sealed class DeviceService
    {
        public const int MaxNameLength = 100;
        public const int MaxAddressLength = 100;

        private readonly HomeRuntime _runtime;
        private readonly IReadOnlyList<IDeviceDiscovery> _discoveries;

        public DeviceService(HomeRuntime runtime, IEnumerable<IDeviceDiscovery> discoveries)
        {
            _runtime = runtime;
            _discoveries = (discoveries ?? Enumerable.Empty<IDeviceDiscovery>()).ToList();
        }

        public Task<IReadOnlyList<Room>> ListRoomsAsync() => _runtime.Repositories.Rooms.ListAsync();

        public async Task<IReadOnlyList<Device>> ListAsync(string roomId)
        {
            var devices = await _runtime.Repositories.Devices.ListAsync().ConfigureAwait(false);
            var selected = string.IsNullOrEmpty(roomId) ? devices : devices.Where(d => string.Equals(d.RoomId, roomId, StringComparison.Ordinal)).ToList();
            foreach (var device in selected) await MarkPairingAsync(device).ConfigureAwait(false);
            return selected;
        }

        public async Task<Device> GetAsync(string deviceId)
        {
            var device = await _runtime.Repositories.Devices.GetAsync(deviceId).ConfigureAwait(false);
            if (device == null)
            {
                throw new NotFoundException("Device", deviceId);
            }

            await MarkPairingAsync(device).ConfigureAwait(false);
            return device;
        }

        /// <summary>A person changes a light. Device-triggered automations run before this returns.</summary>
        public Task<Device> CommandAsync(string deviceId, DeviceCommand command)
        {
            return _runtime.ExclusiveAsync(async () =>
            {
                var device = await GetAsync(deviceId).ConfigureAwait(false);
                DeviceCommands.Validate(device, command);
                var changes = new List<DeviceChange>();
                await _runtime.ApplyCommandAsync(device, command, AutomationChain.Empty, changes).ConfigureAwait(false);
                await _runtime.ProcessChangesAsync(changes).ConfigureAwait(false);
                return await GetAsync(deviceId).ConfigureAwait(false);
            });
        }

        /// <summary>Rename a device or move it to a room (null = no room).</summary>
        public Task<Device> UpdateAsync(string deviceId, string name, string roomId)
        {
            var cleanName = (name ?? string.Empty).Trim();
            if (cleanName.Length == 0 || cleanName.Length > MaxNameLength)
            {
                throw new DomainException(ErrorCodes.ValidationFailed, "name must be 1-" + MaxNameLength + " characters.");
            }

            return _runtime.ExclusiveAsync(async () =>
            {
                var device = await GetAsync(deviceId).ConfigureAwait(false);
                if (roomId != null)
                {
                    var rooms = await _runtime.Repositories.Rooms.ListAsync().ConfigureAwait(false);
                    if (!rooms.Any(r => string.Equals(r.Id, roomId, StringComparison.Ordinal)))
                    {
                        throw new DomainException(ErrorCodes.ValidationFailed, "Room '" + roomId + "' does not exist.");
                    }
                }

                device.Name = cleanName;
                device.RoomId = roomId;
                await _runtime.Repositories.Devices.SaveDefinitionAsync(device).ConfigureAwait(false);
                return await GetAsync(deviceId).ConfigureAwait(false);
            });
        }

        /// <summary>The phone says what changed (home, charging, asleep). Automations it triggers run before this returns.</summary>
        public Task<Device> ReportAsync(string deviceId, DeviceReport report)
        {
            return _runtime.ExclusiveAsync(async () =>
            {
                var device = await GetAsync(deviceId).ConfigureAwait(false);
                DeviceReports.Validate(device, report);
                var changes = new List<DeviceChange>();
                await _runtime.SaveStateAsync(device, DeviceReports.Apply(device, report), true, AutomationChain.Empty, changes).ConfigureAwait(false);
                await _runtime.ProcessChangesAsync(changes).ConfigureAwait(false);
                return await GetAsync(deviceId).ConfigureAwait(false);
            });
        }

        /// <summary>
        /// Runs every discovery (seconds of network I/O, outside the gate), then adds lights it has not seen before
        /// (no room) and updates the address of ones it has. A light found both locally and in the cloud uses the
        /// local driver and the cloud's name.
        /// </summary>
        public async Task<DiscoveryOutcome> DiscoverAsync(CancellationToken cancellationToken)
        {
            var notes = new List<string>();
            var found = new List<DiscoveredDevice>();
            foreach (var discovery in _discoveries)
            {
                var result = await discovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
                notes.AddRange(result.Notes);
                found.AddRange(result.Devices.Where(d => !string.IsNullOrEmpty(d.HardwareId)));
            }

            var merged = found
                .GroupBy(d => d.HardwareId, StringComparer.Ordinal)
                .Select(g =>
                {
                    var local = g.FirstOrDefault(d => d.Driver != DriverKeys.GoveeCloud) ?? g.First();
                    var cloud = g.FirstOrDefault(d => d.Driver == DriverKeys.GoveeCloud);
                    return new DiscoveredDevice { HardwareId = g.Key, Type = local.Type, Driver = local.Driver, Address = local.Address, Name = (cloud ?? local).Name };
                })
                .ToList();

            return await _runtime.ExclusiveAsync(async () =>
            {
                var added = new List<Device>();
                var updated = new List<Device>();
                foreach (var light in merged)
                {
                    var existing = await _runtime.Repositories.Devices.GetByHardwareIdAsync(light.HardwareId).ConfigureAwait(false);
                    if (existing == null)
                    {
                        var device = new Device
                        {
                            Id = Ids.New("light"),
                            Name = Truncate(light.Name, MaxNameLength),
                            RoomId = null,
                            Type = light.Type,
                            Driver = light.Driver,
                            DriverAddress = light.Address,
                            HardwareId = light.HardwareId,
                            Online = true,
                            LastChangedAt = _runtime.Clock.UtcNow,
                        };
                        await _runtime.Repositories.Devices.AddAsync(device).ConfigureAwait(false);
                        added.Add(device);
                    }
                    else if (existing.Driver != light.Driver || existing.DriverAddress != light.Address)
                    {
                        existing.Driver = light.Driver;
                        existing.DriverAddress = light.Address;
                        await _runtime.Repositories.Devices.SaveDefinitionAsync(existing).ConfigureAwait(false);
                        updated.Add(existing);
                    }
                }

                if (merged.Count > 0 && added.Count == 0 && updated.Count == 0)
                {
                    notes.Add("No new lights: every light found is already in Homebase.");
                }

                return new DiscoveryOutcome { Added = added, Updated = updated, Notes = notes };
            }).ConfigureAwait(false);
        }

        /// <summary>Asks a TV to show its pairing code. Saves the address first when one is given.</summary>
        public async Task<string> StartPairingAsync(string deviceId, string address)
        {
            var device = await GetAsync(deviceId).ConfigureAwait(false);
            var driver = Pairable(device);
            if (!string.IsNullOrWhiteSpace(address))
            {
                var clean = address.Trim();
                if (clean.Length > MaxAddressLength || clean.Any(char.IsWhiteSpace))
                {
                    throw new DomainException(ErrorCodes.ValidationFailed, "address must be an IP address or host name.");
                }

                device = await _runtime.ExclusiveAsync(async () =>
                {
                    var current = await GetAsync(deviceId).ConfigureAwait(false);
                    current.DriverAddress = clean;
                    await _runtime.Repositories.Devices.SaveDefinitionAsync(current).ConfigureAwait(false);
                    return current;
                }).ConfigureAwait(false);
            }

            await driver.StartPairingAsync(device).ConfigureAwait(false);
            return "Enter the 6-character code shown on " + device.Name + ".";
        }

        public async Task<Device> ConfirmPairingAsync(string deviceId, string code)
        {
            var device = await GetAsync(deviceId).ConfigureAwait(false);
            var state = await Pairable(device).ConfirmPairingAsync(device, code).ConfigureAwait(false);
            return await _runtime.ExclusiveAsync(async () =>
            {
                var current = await GetAsync(deviceId).ConfigureAwait(false);
                var changes = new List<DeviceChange>();
                await _runtime.SaveStateAsync(current, state, true, AutomationChain.Empty, changes).ConfigureAwait(false);
                await _runtime.ProcessChangesAsync(changes).ConfigureAwait(false);
                return await GetAsync(deviceId).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private IPairableDriver Pairable(Device device)
        {
            var driver = _runtime.Drivers.Resolve(device.Driver) as IPairableDriver;
            if (driver == null)
            {
                throw new DomainException(ErrorCodes.NotPairable, device.Name + " does not need pairing.");
            }

            return driver;
        }

        private async Task MarkPairingAsync(Device device)
        {
            var driver = _runtime.Drivers.Resolve(device.Driver) as IPairableDriver;
            device.NeedsPairing = driver != null && !await driver.IsPairedAsync(device).ConfigureAwait(false);
        }

        private static string Truncate(string value, int max) => value.Length <= max ? value : value.Substring(0, max);
    }

    public sealed class SceneActivation
    {
        public string SceneId { get; set; }

        public IReadOnlyList<ActionResult> Results { get; set; }
    }

    public sealed class SceneService
    {
        private readonly HomeRuntime _runtime;

        public SceneService(HomeRuntime runtime)
        {
            _runtime = runtime;
        }

        public Task<IReadOnlyList<Scene>> ListAsync() => _runtime.Repositories.Scenes.ListAsync();

        public Task<NameLookup> NamesAsync() => _runtime.NamesAsync();

        /// <summary>Applies every action independently; one offline light does not stop the others.</summary>
        public Task<SceneActivation> ActivateAsync(string sceneId)
        {
            return _runtime.ExclusiveAsync(async () =>
            {
                var scene = await _runtime.Repositories.Scenes.GetAsync(sceneId).ConfigureAwait(false);
                if (scene == null)
                {
                    throw new NotFoundException("Scene", sceneId);
                }

                var changes = new List<DeviceChange>();
                var results = await _runtime.ExecuteAsync(scene.Actions, AutomationChain.Empty, changes).ConfigureAwait(false);
                await _runtime.ProcessChangesAsync(changes).ConfigureAwait(false);
                return new SceneActivation { SceneId = scene.Id, Results = results };
            });
        }
    }

    public sealed class Simulation
    {
        public string AutomationId { get; set; }

        public DateTime EvaluatedAt { get; set; }

        public Evaluation Evaluation { get; set; }
    }

    public sealed class AutomationService
    {
        public const int DefaultRunLimit = 50;
        public const int MaxRunLimit = 200;

        private readonly HomeRuntime _runtime;
        private readonly ConcurrentDictionary<string, DateTime> _lastPolled = new ConcurrentDictionary<string, DateTime>(StringComparer.Ordinal);

        public AutomationService(HomeRuntime runtime)
        {
            _runtime = runtime;
        }

        public async Task<IReadOnlyList<Automation>> ListAsync()
        {
            var automations = await _runtime.Repositories.Automations.ListAsync().ConfigureAwait(false);
            foreach (var automation in automations) automation.WaitingUntil = _runtime.WaitingUntil(automation.Id);
            return automations;
        }

        public Task<NameLookup> NamesAsync() => _runtime.NamesAsync();

        public async Task<Automation> GetAsync(string automationId)
        {
            var automation = await _runtime.Repositories.Automations.GetAsync(automationId).ConfigureAwait(false);
            if (automation == null)
            {
                throw new NotFoundException("Automation", automationId);
            }

            automation.WaitingUntil = _runtime.WaitingUntil(automation.Id);
            return automation;
        }

        /// <summary>Turning an automation off also drops a countdown it was waiting on.</summary>
        public Task<Automation> SetEnabledAsync(string automationId, bool enabled)
        {
            return _runtime.ExclusiveAsync(async () =>
            {
                await GetAsync(automationId).ConfigureAwait(false);
                await _runtime.Repositories.Automations.SetEnabledAsync(automationId, enabled).ConfigureAwait(false);
                if (!enabled) _runtime.CancelPending(automationId);
                return await GetAsync(automationId).ConfigureAwait(false);
            });
        }

        /// <summary>Dry run as if the trigger fired now. Reads state only; never executes or records a run.</summary>
        public async Task<Simulation> SimulateAsync(string automationId)
        {
            var automation = await GetAsync(automationId).ConfigureAwait(false);
            var context = await _runtime.ContextAsync(EvaluationReason.Simulation, null).ConfigureAwait(false);
            return new Simulation
            {
                AutomationId = automation.Id,
                EvaluatedAt = context.NowUtc,
                Evaluation = AutomationEngine.Evaluate(automation, context),
            };
        }

        public Task<IReadOnlyList<AutomationRun>> ListRunsAsync(int? limit) =>
            _runtime.Repositories.Automations.ListRunsAsync(Limits.Check(limit, DefaultRunLimit, MaxRunLimit));

        /// <summary>
        /// One scheduler tick: fires due time triggers (each at most once per household minute) and finishes due
        /// forMinutes countdowns — firing if the field still matches, otherwise recording a cancelled run.
        /// </summary>
        public Task<int> TickAsync()
        {
            return _runtime.ExclusiveAsync(async () =>
            {
                var fired = 0;
                var automations = await _runtime.Repositories.Automations.ListAsync().ConfigureAwait(false);
                foreach (var automation in automations.Where(a => a.Enabled && a.Trigger.Kind == TriggerKind.Time))
                {
                    var context = await _runtime.ContextAsync(EvaluationReason.TimeTick, null).ConfigureAwait(false);
                    var evaluation = AutomationEngine.Evaluate(automation, context);
                    if (!evaluation.Triggered)
                    {
                        continue;
                    }

                    var changes = new List<DeviceChange>();
                    await _runtime.RunAsync(automation, evaluation, AutomationChain.Empty.Extend(automation.Id), changes).ConfigureAwait(false);
                    await _runtime.ProcessChangesAsync(changes).ConfigureAwait(false);
                    fired++;
                }

                foreach (var pending in _runtime.TakeDuePending())
                {
                    var automation = await _runtime.Repositories.Automations.GetAsync(pending.AutomationId).ConfigureAwait(false);
                    if (automation == null || !automation.Enabled)
                    {
                        continue;
                    }

                    var context = await _runtime.ContextAsync(EvaluationReason.HeldFor, null, pending).ConfigureAwait(false);
                    var evaluation = AutomationEngine.Evaluate(automation, context);
                    if (!evaluation.Triggered)
                    {
                        await _runtime.RecordCancelledAsync(automation, evaluation.Trace).ConfigureAwait(false);
                        continue;
                    }

                    var changes = new List<DeviceChange>();
                    await _runtime.RunAsync(automation, evaluation, pending.Chain, changes).ConfigureAwait(false);
                    await _runtime.ProcessChangesAsync(changes).ConfigureAwait(false);
                    fired++;
                }

                return fired;
            });
        }

        /// <summary>
        /// Reads every device whose driver polls and is due (per-driver interval), outside the gate so a slow or
        /// unreachable device never blocks commands, then records state/online changes inside it so device-triggered
        /// automations see them (e.g. the TV turning off).
        /// </summary>
        public async Task<int> PollDevicesAsync()
        {
            var now = _runtime.Clock.UtcNow;
            var due = (await _runtime.Repositories.Devices.ListAsync().ConfigureAwait(false))
                .Where(d =>
                {
                    var interval = _runtime.Drivers.Resolve(d.Driver).PollInterval;
                    DateTime last;
                    return interval.HasValue && (!_lastPolled.TryGetValue(d.Id, out last) || now - last >= interval.Value);
                })
                .ToList();

            var readings = await Task.WhenAll(due.Select(async d =>
            {
                _lastPolled[d.Id] = now;
                try
                {
                    return Tuple.Create(d, await _runtime.Drivers.Resolve(d.Driver).ReadAsync(d).ConfigureAwait(false));
                }
                catch (DeviceOfflineException)
                {
                    return Tuple.Create(d, (DeviceState)null);
                }
            })).ConfigureAwait(false);

            return await _runtime.ExclusiveAsync(async () =>
            {
                var updated = 0;
                var changes = new List<DeviceChange>();
                foreach (var reading in readings)
                {
                    var device = await _runtime.Repositories.Devices.GetAsync(reading.Item1.Id).ConfigureAwait(false);
                    if (device == null)
                    {
                        continue;
                    }

                    var online = reading.Item2 != null;
                    var next = online ? DeviceCapabilities.Normalize(device.Type, reading.Item2) : device.State;
                    if (online == device.Online && SameState(device.State, next))
                    {
                        continue;
                    }

                    await _runtime.SaveStateAsync(device, next, online, AutomationChain.Empty, changes).ConfigureAwait(false);
                    updated++;
                }

                await _runtime.ProcessChangesAsync(changes).ConfigureAwait(false);
                return updated;
            }).ConfigureAwait(false);
        }

        private static bool SameState(DeviceState a, DeviceState b) =>
            a.On == b.On && a.Brightness == b.Brightness && a.ColorTempK == b.ColorTempK
            && a.Home == b.Home && a.Charging == b.Charging && a.Asleep == b.Asleep;
    }

    public sealed class Presence
    {
        /// <summary>From the phone; null until it has reported.</summary>
        public bool? Home { get; set; }

        public string PhoneDeviceId { get; set; }

        public string TvDeviceId { get; set; }

        public SunInfo Sun { get; set; }
    }

    public sealed class PresenceService
    {
        private readonly HomeRuntime _runtime;

        public PresenceService(HomeRuntime runtime)
        {
            _runtime = runtime;
        }

        public async Task<Presence> StatusAsync()
        {
            var devices = await _runtime.Repositories.Devices.ListAsync().ConfigureAwait(false);
            var phone = devices.FirstOrDefault(d => d.Type == DeviceType.Phone);
            var tv = devices.FirstOrDefault(d => d.Type == DeviceType.Tv);
            return new Presence
            {
                Home = phone == null ? null : phone.State.Home,
                PhoneDeviceId = phone == null ? null : phone.Id,
                TvDeviceId = tv == null ? null : tv.Id,
                Sun = _runtime.SunNow(),
            };
        }
    }

    internal static class Limits
    {
        public static int Check(int? limit, int defaultValue, int max)
        {
            var value = limit ?? defaultValue;
            if (value < 1 || value > max)
            {
                throw new DomainException(ErrorCodes.ValidationFailed, "limit must be between 1 and " + max + ".");
            }

            return value;
        }
    }
}
