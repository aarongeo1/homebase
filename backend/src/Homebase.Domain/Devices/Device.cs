using System;

namespace Homebase.Domain.Devices
{
    public enum DeviceType
    {
        Light,
        Tv,
        Phone,
    }

    public enum DeviceField
    {
        On,
        Brightness,
        ColorTempK,
        Home,
        Charging,
        Asleep,
    }

    public static class DriverKeys
    {
        public const string Simulated = "simulated";
        public const string GoveeLan = "govee-lan";
        public const string GoveeCloud = "govee-cloud";
        public const string AndroidTv = "android-tv";

        /// <summary>No hardware to call: the device reports its own state (POST /api/devices/{id}/report), e.g. the phone.</summary>
        public const string Push = "push";
    }

    public sealed class Room
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public int SortOrder { get; set; }
    }

    /// <summary>Flat state bag; which fields are meaningful depends on the type (see <see cref="DeviceCapabilities"/>).</summary>
    public sealed class DeviceState
    {
        public bool? On { get; set; }

        public int? Brightness { get; set; }

        /// <summary>White colour temperature in kelvin; null when unknown or the light is showing a colour.</summary>
        public int? ColorTempK { get; set; }

        public bool? Home { get; set; }

        public bool? Charging { get; set; }

        public bool? Asleep { get; set; }

        public DeviceState Clone() => (DeviceState)MemberwiseClone();
    }

    public sealed class Device
    {
        public Device()
        {
            State = new DeviceState();
            Driver = DriverKeys.Simulated;
        }

        public string Id { get; set; }

        public string Name { get; set; }

        /// <summary>Null for a light that was found but not put in a room yet.</summary>
        public string RoomId { get; set; }

        public DeviceType Type { get; set; }

        /// <summary>Driver key, e.g. "govee-lan" (see <see cref="DriverKeys"/>).</summary>
        public string Driver { get; set; }

        /// <summary>Driver-specific address: an IP for govee-lan and android-tv, "SKU|device" for govee-cloud, null otherwise.</summary>
        public string DriverAddress { get; set; }

        /// <summary>The hardware's own id (Govee device id), so discovery recognises a light it has seen before. Null if none.</summary>
        public string HardwareId { get; set; }

        public bool Online { get; set; }

        public DeviceState State { get; set; }

        public DateTime LastChangedAt { get; set; }

        /// <summary>Not stored: set by the application layer from the driver (a TV that has not been paired yet).</summary>
        public bool NeedsPairing { get; set; }
    }
}
