using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Homebase.Devices;
using Homebase.Domain;
using Homebase.Domain.Devices;

namespace Homebase.Testing
{
    /// <summary>Stands in for the Android TV driver: tests set <see cref="On"/> and <see cref="Reachable"/>, then poll.</summary>
    public sealed class FakeTvDriver : IPairableDriver
    {
        public const string CorrectCode = "ABC123";

        public bool? On { get; set; } = false;

        public bool Reachable { get; set; } = true;

        public bool Paired { get; set; } = true;

        public List<string> PairingStartedFor { get; } = new List<string>();

        public string Key => DriverKeys.AndroidTv;

        public TimeSpan? PollInterval => TimeSpan.FromSeconds(15);

        public Task<DeviceState> ApplyAsync(Device device, DeviceCommand command)
        {
            throw new DomainException(ErrorCodes.ReadOnlyDevice, device.Name + " is watched, not controlled.");
        }

        public Task<DeviceState> ReadAsync(Device device)
        {
            if (!Reachable || !Paired)
            {
                throw new DeviceOfflineException(device.Name, Paired ? "no answer from the TV" : "not paired yet");
            }

            return Task.FromResult(new DeviceState { On = On });
        }

        public Task<bool> IsPairedAsync(Device device) => Task.FromResult(Paired);

        public Task StartPairingAsync(Device device)
        {
            PairingStartedFor.Add(device.DriverAddress);
            return Task.CompletedTask;
        }

        public Task<DeviceState> ConfirmPairingAsync(Device device, string code)
        {
            if (code != CorrectCode)
            {
                throw new DomainException(ErrorCodes.PairingFailed, "That code does not match the one on the TV.");
            }

            Paired = true;
            return ReadAsync(device);
        }
    }
}

namespace Homebase.Testing
{
    /// <summary>Behaves like the simulated driver under another key (e.g. govee-lan), for lights that discovery adds.</summary>
    public sealed class FakeLightDriver : IDeviceDriver
    {
        public FakeLightDriver(string key)
        {
            Key = key;
        }

        public string Key { get; }

        public TimeSpan? PollInterval => null;

        public Task<DeviceState> ApplyAsync(Device device, DeviceCommand command) => Task.FromResult(DeviceCommands.Apply(device, command));

        public Task<DeviceState> ReadAsync(Device device) => Task.FromResult(device.State.Clone());
    }
}
