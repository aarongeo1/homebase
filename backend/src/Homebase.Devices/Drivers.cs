using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Homebase.Domain;
using Homebase.Domain.Devices;

namespace Homebase.Devices
{
    /// <summary>
    /// How Homebase talks to one kind of hardware. Implementations receive an already-validated command
    /// (DeviceCommands.Validate) and return the device's full state afterwards. Throw DeviceOfflineException
    /// when the device does not answer; never throw for business rules (those are validated before the driver runs).
    /// </summary>
    public interface IDeviceDriver
    {
        /// <summary>Matches Device.Driver, e.g. "govee-lan".</summary>
        string Key { get; }

        /// <summary>How often the scheduler reads the device; null = never (state only changes through Homebase or reports).</summary>
        TimeSpan? PollInterval { get; }

        Task<DeviceState> ApplyAsync(Device device, DeviceCommand command);

        Task<DeviceState> ReadAsync(Device device);
    }

    /// <summary>A driver that must be paired once before it can read the device (the Android TV remote protocol).</summary>
    public interface IPairableDriver : IDeviceDriver
    {
        Task<bool> IsPairedAsync(Device device);

        /// <summary>Makes the device show a code. Throws DomainException(pairing_failed) when it cannot.</summary>
        Task StartPairingAsync(Device device);

        /// <summary>Completes pairing with the code the device shows and returns its state.</summary>
        Task<DeviceState> ConfirmPairingAsync(Device device, string code);
    }

    /// <summary>Finds devices on the network or in a vendor account. Never throws for "nothing found"; it explains in Notes.</summary>
    public interface IDeviceDiscovery
    {
        Task<DiscoveryResult> DiscoverAsync(CancellationToken cancellationToken);
    }

    public sealed class DiscoveredDevice
    {
        public string HardwareId { get; set; }

        public string Name { get; set; }

        public DeviceType Type { get; set; }

        public string Driver { get; set; }

        public string Address { get; set; }
    }

    public sealed class DiscoveryResult
    {
        public List<DiscoveredDevice> Devices { get; } = new List<DiscoveredDevice>();

        public List<string> Notes { get; } = new List<string>();
    }

    public sealed class DriverRegistry
    {
        private readonly Dictionary<string, IDeviceDriver> _drivers;

        public DriverRegistry(IEnumerable<IDeviceDriver> drivers)
        {
            _drivers = drivers.ToDictionary(d => d.Key, StringComparer.Ordinal);
        }

        public IEnumerable<string> Keys => _drivers.Keys;

        /// <summary>An unknown key is a configuration error (the database CHECK constraint lists the valid keys).</summary>
        public IDeviceDriver Resolve(string key)
        {
            IDeviceDriver driver;
            if (key == null || !_drivers.TryGetValue(key, out driver))
            {
                throw new InvalidOperationException("No device driver registered for '" + key + "'.");
            }

            return driver;
        }
    }

    /// <summary>For tests and for trying the app without hardware: state lives only in the database.</summary>
    public sealed class SimulatedDriver : IDeviceDriver
    {
        public string Key => DriverKeys.Simulated;

        public TimeSpan? PollInterval => null;

        public Task<DeviceState> ApplyAsync(Device device, DeviceCommand command) => Task.FromResult(DeviceCommands.Apply(device, command));

        public Task<DeviceState> ReadAsync(Device device) => Task.FromResult(device.State.Clone());
    }

    /// <summary>The phone: it reports its own state, so there is nothing to call or poll.</summary>
    public sealed class PushDriver : IDeviceDriver
    {
        public string Key => DriverKeys.Push;

        public TimeSpan? PollInterval => null;

        public Task<DeviceState> ApplyAsync(Device device, DeviceCommand command)
        {
            throw new DomainException(ErrorCodes.ReadOnlyDevice, device.Name + " reports its own state and cannot be controlled.");
        }

        public Task<DeviceState> ReadAsync(Device device) => Task.FromResult(device.State.Clone());
    }
}
