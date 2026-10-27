using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Homebase.Domain.Automations;
using Homebase.Domain.Devices;

namespace Homebase.Domain.Abstractions
{
    // Ports implemented by Homebase.Data (SQLite) and by tests/Homebase.Testing (in memory).
    // Get* methods return null when the row does not exist; callers decide whether that is a 404.

    public interface IDatabaseHealthCheck
    {
        Task<bool> CanConnectAsync();
    }

    public interface IRoomRepository
    {
        /// <summary>Ordered by sort order.</summary>
        Task<IReadOnlyList<Room>> ListAsync();
    }

    public interface IDeviceRepository
    {
        /// <summary>Ordered by room sort order (devices without a room last), then name.</summary>
        Task<IReadOnlyList<Device>> ListAsync();

        Task<Device> GetAsync(string id);

        Task<Device> GetByHardwareIdAsync(string hardwareId);

        Task AddAsync(Device device);

        /// <summary>Persists Online, State and LastChangedAt only.</summary>
        Task SaveStateAsync(Device device);

        /// <summary>Persists Name, RoomId, Driver and DriverAddress only.</summary>
        Task SaveDefinitionAsync(Device device);
    }

    public interface ISceneRepository
    {
        Task<IReadOnlyList<Scene>> ListAsync();

        Task<Scene> GetAsync(string id);
    }

    public interface IAutomationRepository
    {
        /// <summary>Ordered by name; LastRunAt/LastOutcome come from the newest run.</summary>
        Task<IReadOnlyList<Automation>> ListAsync();

        Task<Automation> GetAsync(string id);

        Task SetEnabledAsync(string id, bool enabled);

        Task AddRunAsync(AutomationRun run);

        /// <summary>Newest first.</summary>
        Task<IReadOnlyList<AutomationRun>> ListRunsAsync(int limit);
    }

    /// <summary>Small values the drivers keep between restarts (e.g. a paired TV's client certificate). Null when missing.</summary>
    public interface ISettingsRepository
    {
        Task<string> GetAsync(string key);

        Task SetAsync(string key, string value);
    }
}
