using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Homebase.Devices;
using Homebase.Domain;
using Homebase.Domain.Abstractions;
using Homebase.Domain.Automations;
using Homebase.Domain.Devices;
using Homebase.Domain.Sun;

namespace Homebase.Application
{
    public sealed class HomeRepositories
    {
        public IRoomRepository Rooms { get; set; }

        public IDeviceRepository Devices { get; set; }

        public ISceneRepository Scenes { get; set; }

        public IAutomationRepository Automations { get; set; }

        public ISettingsRepository Settings { get; set; }
    }

    public sealed class ActionResult
    {
        public HomeAction Action { get; set; }

        public bool Ok { get; set; }

        public string ErrorCode { get; set; }

        public string Message { get; set; }
    }

    /// <summary>
    /// The household's single writer. Every state-changing entry point runs inside <see cref="ExclusiveAsync{T}"/>, so a
    /// command, a phone report, the scheduler tick and the automations they cause never interleave. Methods without
    /// "Exclusive" assume the caller already holds the gate (calling ExclusiveAsync again from inside would deadlock).
    /// </summary>
    public sealed class HomeRuntime
    {
        private const int MaxRunsPerCascade = 25;
        private const int MaxSceneNesting = 2;

        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        // forMinutes countdowns by automation id. In memory on purpose: after a restart a countdown simply starts again
        // on the next change. Locked on itself because listings read it outside the gate.
        private readonly Dictionary<string, PendingTrigger> _pending = new Dictionary<string, PendingTrigger>(StringComparer.Ordinal);

        public HomeRuntime(HomeRepositories repositories, DriverRegistry drivers, IHouseholdClock clock, GeoLocation location)
        {
            Repositories = repositories;
            Drivers = drivers;
            Clock = clock;
            Location = location;
        }

        public HomeRepositories Repositories { get; }

        public DriverRegistry Drivers { get; }

        public IHouseholdClock Clock { get; }

        /// <summary>Null when HOMEBASE_LATITUDE/HOMEBASE_LONGITUDE are not set.</summary>
        public GeoLocation Location { get; }

        public async Task<T> ExclusiveAsync<T>(Func<Task<T>> work)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                return await work().ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        public SunInfo SunNow() => SunCalculator.Info(Clock.UtcNow, Location, Clock.TimeZone);

        public DateTime? WaitingUntil(string automationId)
        {
            lock (_pending)
            {
                PendingTrigger pending;
                return _pending.TryGetValue(automationId, out pending) ? pending.DueAt : (DateTime?)null;
            }
        }

        public void CancelPending(string automationId)
        {
            lock (_pending)
            {
                _pending.Remove(automationId);
            }
        }

        /// <summary>Removes and returns every countdown that is due.</summary>
        public IReadOnlyList<PendingTrigger> TakeDuePending()
        {
            lock (_pending)
            {
                var due = _pending.Values.Where(p => p.DueAt <= Clock.UtcNow).OrderBy(p => p.DueAt).ToList();
                foreach (var pending in due) _pending.Remove(pending.AutomationId);
                return due;
            }
        }

        public async Task<NameLookup> NamesAsync()
        {
            var devices = await Repositories.Devices.ListAsync().ConfigureAwait(false);
            var scenes = await Repositories.Scenes.ListAsync().ConfigureAwait(false);
            var rooms = await Repositories.Rooms.ListAsync().ConfigureAwait(false);
            return NameLookup.From(devices, scenes, rooms);
        }

        public async Task<EvaluationContext> ContextAsync(EvaluationReason reason, DeviceChange change, PendingTrigger pending = null)
        {
            var devices = await Repositories.Devices.ListAsync().ConfigureAwait(false);
            var scenes = await Repositories.Scenes.ListAsync().ConfigureAwait(false);
            var rooms = await Repositories.Rooms.ListAsync().ConfigureAwait(false);
            return new EvaluationContext
            {
                NowUtc = Clock.UtcNow,
                TimeZone = Clock.TimeZone,
                Devices = devices.ToDictionary(d => d.Id, StringComparer.Ordinal),
                Reason = reason,
                Change = change,
                Pending = pending,
                Sun = SunNow(),
                Names = NameLookup.From(devices, scenes, rooms),
            };
        }

        /// <summary>Runs the driver, persists the new state and records a DeviceChange when a trigger field changed.</summary>
        public async Task<Device> ApplyCommandAsync(Device device, DeviceCommand command, AutomationChain chain, List<DeviceChange> changes)
        {
            DeviceState next;
            try
            {
                next = await Drivers.Resolve(device.Driver).ApplyAsync(device, command).ConfigureAwait(false);
            }
            catch (DeviceOfflineException)
            {
                device.Online = false;
                device.LastChangedAt = Clock.UtcNow;
                await Repositories.Devices.SaveStateAsync(device).ConfigureAwait(false);
                throw;
            }

            return await SaveStateAsync(device, next, true, chain, changes).ConfigureAwait(false);
        }

        /// <summary>Stores a new state (from a driver, a report or pairing) and records a DeviceChange if a trigger field changed.</summary>
        public async Task<Device> SaveStateAsync(Device device, DeviceState next, bool online, AutomationChain chain, List<DeviceChange> changes)
        {
            var previous = device.State.Clone();
            device.State = DeviceCapabilities.Normalize(device.Type, next);
            device.Online = online;
            device.LastChangedAt = Clock.UtcNow;
            await Repositories.Devices.SaveStateAsync(device).ConfigureAwait(false);

            var change = DeviceChange.Detect(device, previous, chain);
            if (change != null)
            {
                changes.Add(change);
            }

            return device;
        }

        /// <summary>Applies actions for an automation or scene. Each action is independent: a failure never stops the rest.</summary>
        public async Task<IReadOnlyList<ActionResult>> ExecuteAsync(IReadOnlyList<HomeAction> actions, AutomationChain chain, List<DeviceChange> changes, int sceneDepth = 0)
        {
            var results = new List<ActionResult>();
            foreach (var action in actions)
            {
                results.Add(await ExecuteOneAsync(action, chain, changes, sceneDepth).ConfigureAwait(false));
            }

            return results;
        }

        /// <summary>
        /// Evaluates device-triggered automations for each change, breadth first. A change that ends a forMinutes
        /// countdown early cancels it (recorded as a cancelled run). Changes those automations cause are queued with
        /// the extended chain; the engine's loop guard (depth 3, no self re-trigger) ends every cascade.
        /// </summary>
        public async Task ProcessChangesAsync(IEnumerable<DeviceChange> initialChanges)
        {
            var queue = new Queue<DeviceChange>(initialChanges);
            var runs = 0;
            while (queue.Count > 0 && runs < MaxRunsPerCascade)
            {
                var change = queue.Dequeue();
                var automations = await Repositories.Automations.ListAsync().ConfigureAwait(false);
                foreach (var automation in automations.Where(a => a.Enabled && a.Trigger.Kind == TriggerKind.Device
                    && string.Equals(a.Trigger.DeviceId, change.Device.Id, StringComparison.Ordinal)))
                {
                    PendingTrigger pending;
                    lock (_pending)
                    {
                        _pending.TryGetValue(automation.Id, out pending);
                    }

                    if (pending != null && !AutomationEngine.TriggerMatches(automation.Trigger, change.Current))
                    {
                        CancelPending(automation.Id);
                        await RecordCancelledAsync(automation, pending, change.Device).ConfigureAwait(false);
                        runs++;
                        continue;
                    }

                    var context = await ContextAsync(EvaluationReason.DeviceChange, change).ConfigureAwait(false);
                    var evaluation = AutomationEngine.Evaluate(automation, context);
                    if (evaluation.Deferred)
                    {
                        lock (_pending)
                        {
                            if (!_pending.ContainsKey(automation.Id))
                            {
                                _pending[automation.Id] = new PendingTrigger
                                {
                                    AutomationId = automation.Id,
                                    DeviceId = change.Device.Id,
                                    Since = Clock.UtcNow,
                                    DueAt = Clock.UtcNow.AddMinutes(automation.Trigger.ForMinutes ?? 0),
                                    Chain = change.Chain.Extend(automation.Id),
                                };
                            }
                        }

                        continue;
                    }

                    if (!evaluation.Triggered)
                    {
                        continue;
                    }

                    var newChanges = new List<DeviceChange>();
                    await RunAsync(automation, evaluation, change.Chain.Extend(automation.Id), newChanges).ConfigureAwait(false);
                    runs++;
                    foreach (var next in newChanges)
                    {
                        queue.Enqueue(next);
                    }
                }
            }
        }

        /// <summary>Executes a triggered evaluation and persists the run with its trace.</summary>
        public async Task<AutomationRun> RunAsync(Automation automation, Evaluation evaluation, AutomationChain chain, List<DeviceChange> changes)
        {
            var names = await NamesAsync().ConfigureAwait(false);
            var run = new AutomationRun
            {
                Id = Ids.New("run"),
                AutomationId = automation.Id,
                AutomationName = automation.Name,
                FiredAt = Clock.UtcNow,
                Trace = evaluation.Trace.ToList(),
            };

            var failed = 0;
            if (evaluation.WouldRun)
            {
                var results = await ExecuteAsync(evaluation.Planned, chain, changes).ConfigureAwait(false);
                foreach (var result in results.Where(r => !r.Ok))
                {
                    failed++;
                    run.Trace.Add(new TraceStep("Result: " + Describe.Action(result.Action, names), false, result.Message + " (" + result.ErrorCode + ")"));
                }

                run.ActionsExecuted = results.Count(r => r.Ok);
            }

            run.Outcome = AutomationEngine.DecideOutcome(evaluation.WouldRun, failed);
            await Repositories.Automations.AddRunAsync(run).ConfigureAwait(false);
            return run;
        }

        /// <summary>A countdown ended without firing; the run explains why so "why didn't it turn off?" has an answer.</summary>
        public async Task<AutomationRun> RecordCancelledAsync(Automation automation, IList<TraceStep> trace)
        {
            var run = new AutomationRun
            {
                Id = Ids.New("run"),
                AutomationId = automation.Id,
                AutomationName = automation.Name,
                FiredAt = Clock.UtcNow,
                Outcome = RunOutcome.Cancelled,
                Trace = trace.ToList(),
            };
            await Repositories.Automations.AddRunAsync(run).ConfigureAwait(false);
            return run;
        }

        private async Task RecordCancelledAsync(Automation automation, PendingTrigger pending, Device device)
        {
            var names = await NamesAsync().ConfigureAwait(false);
            var waited = (int)Math.Floor((Clock.UtcNow - pending.Since).TotalMinutes);
            var detail = names.Device(device.Id) + " stopped being " + Describe.StateWord(automation.Trigger.Field, automation.Trigger.EqualsValue)
                + " after " + waited + " of " + automation.Trigger.ForMinutes + " min.";
            await RecordCancelledAsync(automation, new[] { new TraceStep("Trigger: " + Describe.Trigger(automation.Trigger, names), false, detail) }).ConfigureAwait(false);
        }

        private async Task<ActionResult> ExecuteOneAsync(HomeAction action, AutomationChain chain, List<DeviceChange> changes, int sceneDepth)
        {
            var result = new ActionResult { Action = action };
            try
            {
                switch (action.Kind)
                {
                    case ActionKind.Scene:
                        return await ExecuteSceneAsync(action, chain, changes, sceneDepth).ConfigureAwait(false);

                    case ActionKind.Room:
                        return await ExecuteRoomAsync(action, chain, changes).ConfigureAwait(false);

                    default:
                        var device = await Repositories.Devices.GetAsync(action.DeviceId).ConfigureAwait(false);
                        if (device == null)
                        {
                            throw new NotFoundException("Device", action.DeviceId);
                        }

                        DeviceCommands.Validate(device, action.Command);
                        await ApplyCommandAsync(device, action.Command, chain, changes).ConfigureAwait(false);
                        break;
                }

                result.Ok = true;
            }
            catch (DomainException ex)
            {
                result.ErrorCode = ex.Code;
                result.Message = ex.Message;
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                // One broken light must not stop the remaining actions; the run records it as failed.
                result.ErrorCode = ErrorCodes.InternalError;
                result.Message = "Unexpected error: " + ex.GetType().Name;
            }

            return result;
        }

        /// <summary>Every light in the room; the action fails (listing which) if any light failed, but the others still change.</summary>
        private async Task<ActionResult> ExecuteRoomAsync(HomeAction action, AutomationChain chain, List<DeviceChange> changes)
        {
            var rooms = await Repositories.Rooms.ListAsync().ConfigureAwait(false);
            if (!rooms.Any(r => string.Equals(r.Id, action.RoomId, StringComparison.Ordinal)))
            {
                throw new NotFoundException("Room", action.RoomId);
            }

            var lights = (await Repositories.Devices.ListAsync().ConfigureAwait(false))
                .Where(d => d.Type == DeviceType.Light && string.Equals(d.RoomId, action.RoomId, StringComparison.Ordinal))
                .ToList();

            var problems = new List<DomainException>();
            foreach (var light in lights)
            {
                try
                {
                    DeviceCommands.Validate(light, action.Command);
                    await ApplyCommandAsync(light, action.Command, chain, changes).ConfigureAwait(false);
                }
                catch (DomainException ex)
                {
                    problems.Add(ex);
                }
            }

            return new ActionResult
            {
                Action = action,
                Ok = problems.Count == 0,
                ErrorCode = problems.Count == 0 ? null : problems[0].Code,
                Message = problems.Count == 0 ? null : string.Join(" ", problems.Select(p => p.Message)),
            };
        }

        private async Task<ActionResult> ExecuteSceneAsync(HomeAction action, AutomationChain chain, List<DeviceChange> changes, int sceneDepth)
        {
            if (sceneDepth >= MaxSceneNesting)
            {
                throw new DomainException(ErrorCodes.ValidationFailed, "Scenes may nest at most " + MaxSceneNesting + " levels.");
            }

            var scene = await Repositories.Scenes.GetAsync(action.SceneId).ConfigureAwait(false);
            if (scene == null)
            {
                throw new NotFoundException("Scene", action.SceneId);
            }

            var inner = await ExecuteAsync(scene.Actions, chain, changes, sceneDepth + 1).ConfigureAwait(false);
            var firstProblem = inner.FirstOrDefault(r => !r.Ok);
            return new ActionResult
            {
                Action = action,
                Ok = firstProblem == null,
                ErrorCode = firstProblem == null ? null : firstProblem.ErrorCode,
                Message = firstProblem == null ? null : "Scene " + scene.Name + ": " + firstProblem.Message,
            };
        }
    }

    internal static class Ids
    {
        public static string New(string prefix) => prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 16);
    }
}
