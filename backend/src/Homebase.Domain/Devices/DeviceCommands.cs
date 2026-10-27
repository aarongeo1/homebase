using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Homebase.Domain.Devices
{
    public sealed class DeviceCommand
    {
        public bool? On { get; set; }

        public int? Brightness { get; set; }

        public int? ColorTempK { get; set; }

        public IEnumerable<DeviceField> Fields()
        {
            if (On.HasValue) yield return DeviceField.On;
            if (Brightness.HasValue) yield return DeviceField.Brightness;
            if (ColorTempK.HasValue) yield return DeviceField.ColorTempK;
        }

        public bool IsEmpty => !Fields().Any();
    }

    public static class DeviceCommands
    {
        public const int MinBrightness = 1;
        public const int MaxBrightness = 100;
        public const int MinColorTempK = 2000;
        public const int MaxColorTempK = 9000;

        /// <summary>
        /// Checks, in order: the TV and phone are read-only (read_only_device); the device is online (device_offline);
        /// every field is supported and in range (validation_failed).
        /// </summary>
        public static void Validate(Device device, DeviceCommand command)
        {
            if (DeviceCapabilities.IsReadOnly(device.Type))
            {
                throw new DomainException(ErrorCodes.ReadOnlyDevice, device.Name + " can be watched but not controlled.");
            }

            if (!device.Online)
            {
                throw new DeviceOfflineException(device.Name, "it is not responding");
            }

            if (command == null || command.IsEmpty)
            {
                throw new DomainException(ErrorCodes.ValidationFailed, "The command has no fields to change.");
            }

            var unsupported = command.Fields().Where(f => !DeviceCapabilities.Accepts(device.Type, f)).ToList();
            if (unsupported.Count > 0)
            {
                throw new DomainException(
                    ErrorCodes.ValidationFailed,
                    device.Name + " does not support " + string.Join(", ", unsupported.Select(FieldNames.Of)) + ".");
            }

            if (command.Brightness.HasValue && (command.Brightness < MinBrightness || command.Brightness > MaxBrightness))
            {
                throw new DomainException(ErrorCodes.ValidationFailed, "Brightness must be between 1 and 100.");
            }

            if (command.Brightness.HasValue && command.On == false)
            {
                throw new DomainException(ErrorCodes.ValidationFailed, "Setting brightness turns a light on; do not combine it with on=false.");
            }

            if (command.ColorTempK.HasValue && (command.ColorTempK < MinColorTempK || command.ColorTempK > MaxColorTempK))
            {
                throw new DomainException(
                    ErrorCodes.ValidationFailed,
                    "Colour temperature must be between " + MinColorTempK.ToString(CultureInfo.InvariantCulture)
                        + " and " + MaxColorTempK.ToString(CultureInfo.InvariantCulture) + " K.");
            }
        }

        /// <summary>The desired state after the command. Brightness implies on.</summary>
        public static DeviceState Apply(Device device, DeviceCommand command)
        {
            var state = device.State.Clone();
            if (command.On.HasValue) state.On = command.On;
            if (command.Brightness.HasValue)
            {
                state.Brightness = command.Brightness;
                state.On = true;
            }

            if (command.ColorTempK.HasValue) state.ColorTempK = command.ColorTempK;
            return state;
        }
    }

    /// <summary>What a push device (the phone) says about itself. Only the fields that changed need to be sent.</summary>
    public sealed class DeviceReport
    {
        public bool? Home { get; set; }

        public bool? Charging { get; set; }

        public bool? Asleep { get; set; }

        public IEnumerable<DeviceField> Fields()
        {
            if (Home.HasValue) yield return DeviceField.Home;
            if (Charging.HasValue) yield return DeviceField.Charging;
            if (Asleep.HasValue) yield return DeviceField.Asleep;
        }

        public bool IsEmpty => !Fields().Any();
    }

    public static class DeviceReports
    {
        /// <summary>Only push devices report (not_reportable); the report needs at least one field the type has (validation_failed).</summary>
        public static void Validate(Device device, DeviceReport report)
        {
            if (device.Driver != DriverKeys.Push)
            {
                throw new DomainException(ErrorCodes.NotReportable, device.Name + " is read by Homebase; it cannot report its own state.");
            }

            if (report == null || report.IsEmpty)
            {
                throw new DomainException(ErrorCodes.ValidationFailed, "The report has no fields. Send e.g. { \"home\": false }.");
            }

            var unsupported = report.Fields().Where(f => !DeviceCapabilities.AcceptsReport(device.Type, f)).ToList();
            if (unsupported.Count > 0)
            {
                throw new DomainException(
                    ErrorCodes.ValidationFailed,
                    device.Name + " cannot report " + string.Join(", ", unsupported.Select(FieldNames.Of)) + ".");
            }
        }

        public static DeviceState Apply(Device device, DeviceReport report)
        {
            var state = device.State.Clone();
            if (report.Home.HasValue) state.Home = report.Home;
            if (report.Charging.HasValue) state.Charging = report.Charging;
            if (report.Asleep.HasValue) state.Asleep = report.Asleep;
            return state;
        }
    }

    /// <summary>Field names as they appear in the API (camelCase), for messages.</summary>
    public static class FieldNames
    {
        public static string Of(DeviceField field)
        {
            var name = field.ToString();
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}
