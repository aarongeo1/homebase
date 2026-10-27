using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Homebase.Application;
using Homebase.Domain;
using Homebase.Domain.Abstractions;
using Homebase.Domain.Automations;
using Homebase.Domain.Devices;

namespace Homebase.Testing
{
    /// <summary>All repository ports in memory. Reads return copies so tests behave like a real database.</summary>
    public sealed class InMemoryHome :
        IRoomRepository, IDeviceRepository, ISceneRepository, IAutomationRepository, ISettingsRepository, IDatabaseHealthCheck
    {
        public List<Room> Rooms { get; } = new List<Room>();

        public List<Device> Devices { get; } = new List<Device>();

        public List<Scene> Scenes { get; } = new List<Scene>();

        public List<Automation> Automations { get; } = new List<Automation>();

        public List<AutomationRun> Runs { get; } = new List<AutomationRun>();

        public Dictionary<string, string> Settings { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public bool DatabaseUp { get; set; } = true;

        public HomeRepositories AsRepositories() => new HomeRepositories
        {
            Rooms = this,
            Devices = this,
            Scenes = this,
            Automations = this,
            Settings = this,
        };

        public Device Device(string id) => Devices.Single(d => d.Id == id);

        public IEnumerable<AutomationRun> RunsOf(string automationId) => Runs.Where(r => r.AutomationId == automationId);

        Task<IReadOnlyList<Room>> IRoomRepository.ListAsync() => Result<IReadOnlyList<Room>>(Rooms.OrderBy(r => r.SortOrder).ToList());

        Task<IReadOnlyList<Device>> IDeviceRepository.ListAsync() =>
            Result<IReadOnlyList<Device>>(Devices
                .OrderBy(d => Rooms.Where(r => r.Id == d.RoomId).Select(r => (int?)r.SortOrder).FirstOrDefault() ?? int.MaxValue)
                .ThenBy(d => d.Name, StringComparer.Ordinal)
                .Select(Copy)
                .ToList());

        Task<Device> IDeviceRepository.GetAsync(string id) => Result(Devices.Where(d => d.Id == id).Select(Copy).FirstOrDefault());

        Task<Device> IDeviceRepository.GetByHardwareIdAsync(string hardwareId) =>
            Result(Devices.Where(d => d.HardwareId != null && d.HardwareId == hardwareId).Select(Copy).FirstOrDefault());

        Task IDeviceRepository.AddAsync(Device device)
        {
            if (Devices.Any(d => d.Id == device.Id || (device.HardwareId != null && d.HardwareId == device.HardwareId)))
            {
                throw new InvalidOperationException("Duplicate device " + device.Id);
            }

            Devices.Add(Copy(device));
            return Task.CompletedTask;
        }

        Task IDeviceRepository.SaveStateAsync(Device device)
        {
            var stored = Device(device.Id);
            stored.Online = device.Online;
            stored.State = device.State.Clone();
            stored.LastChangedAt = device.LastChangedAt;
            return Task.CompletedTask;
        }

        Task IDeviceRepository.SaveDefinitionAsync(Device device)
        {
            var stored = Device(device.Id);
            stored.Name = device.Name;
            stored.RoomId = device.RoomId;
            stored.Driver = device.Driver;
            stored.DriverAddress = device.DriverAddress;
            return Task.CompletedTask;
        }

        Task<IReadOnlyList<Scene>> ISceneRepository.ListAsync() => Result<IReadOnlyList<Scene>>(Scenes.OrderBy(s => s.Name, StringComparer.Ordinal).ToList());

        Task<Scene> ISceneRepository.GetAsync(string id) => Result(Scenes.FirstOrDefault(s => s.Id == id));

        Task<IReadOnlyList<Automation>> IAutomationRepository.ListAsync() =>
            Result<IReadOnlyList<Automation>>(Automations.OrderBy(a => a.Name, StringComparer.Ordinal).Select(WithLastRun).ToList());

        Task<Automation> IAutomationRepository.GetAsync(string id) => Result(Automations.Where(a => a.Id == id).Select(WithLastRun).FirstOrDefault());

        Task IAutomationRepository.SetEnabledAsync(string id, bool enabled)
        {
            Automations.Single(a => a.Id == id).Enabled = enabled;
            return Task.CompletedTask;
        }

        Task IAutomationRepository.AddRunAsync(AutomationRun run)
        {
            Runs.Add(run);
            return Task.CompletedTask;
        }

        Task<IReadOnlyList<AutomationRun>> IAutomationRepository.ListRunsAsync(int limit) =>
            Result<IReadOnlyList<AutomationRun>>(Runs.OrderByDescending(r => r.FiredAt).Take(limit).ToList());

        Task<string> ISettingsRepository.GetAsync(string key)
        {
            string value;
            return Result(Settings.TryGetValue(key, out value) ? value : null);
        }

        Task ISettingsRepository.SetAsync(string key, string value)
        {
            Settings[key] = value;
            return Task.CompletedTask;
        }

        Task<bool> IDatabaseHealthCheck.CanConnectAsync() => Result(DatabaseUp);

        private static Task<T> Result<T>(T value) => Task.FromResult(value);

        private static Device Copy(Device d) => new Device
        {
            Id = d.Id,
            Name = d.Name,
            RoomId = d.RoomId,
            Type = d.Type,
            Driver = d.Driver,
            DriverAddress = d.DriverAddress,
            HardwareId = d.HardwareId,
            Online = d.Online,
            State = d.State.Clone(),
            LastChangedAt = d.LastChangedAt,
        };

        private Automation WithLastRun(Automation a)
        {
            var last = Runs.Where(r => r.AutomationId == a.Id).OrderByDescending(r => r.FiredAt).FirstOrDefault();
            return new Automation
            {
                Id = a.Id,
                Name = a.Name,
                Description = a.Description,
                Enabled = a.Enabled,
                Trigger = a.Trigger,
                Conditions = a.Conditions,
                Actions = a.Actions,
                LastRunAt = last == null ? (DateTime?)null : last.FiredAt,
                LastOutcome = last == null ? (RunOutcome?)null : last.Outcome,
            };
        }
    }

    public sealed class FixedClock : IClock
    {
        public FixedClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; set; }

        public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
    }
}
