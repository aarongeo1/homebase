using System.Collections.Generic;
using System.Linq;

namespace Homebase.Domain.Devices
{
    /// <summary>
    /// Which state fields each device type has (docs/GLOSSARY.md "Device capabilities"), which of them a command may set,
    /// and which a device may report about itself. Fields a type does not support are always null.
    /// </summary>
    public static class DeviceCapabilities
    {
        private static readonly Dictionary<DeviceType, DeviceField[]> StateFields = new Dictionary<DeviceType, DeviceField[]>
        {
            { DeviceType.Light, new[] { DeviceField.On, DeviceField.Brightness, DeviceField.ColorTempK } },
            { DeviceType.Tv, new[] { DeviceField.On } },
            { DeviceType.Phone, new[] { DeviceField.Home, DeviceField.Charging, DeviceField.Asleep } },
        };

        private static readonly Dictionary<DeviceType, DeviceField[]> CommandFields = new Dictionary<DeviceType, DeviceField[]>
        {
            { DeviceType.Light, new[] { DeviceField.On, DeviceField.Brightness, DeviceField.ColorTempK } },
        };

        private static readonly Dictionary<DeviceType, DeviceField[]> ReportFields = new Dictionary<DeviceType, DeviceField[]>
        {
            { DeviceType.Phone, new[] { DeviceField.Home, DeviceField.Charging, DeviceField.Asleep } },
        };

        public static IReadOnlyList<DeviceField> FieldsOf(DeviceType type) => StateFields[type];

        public static bool Supports(DeviceType type, DeviceField field) => StateFields[type].Contains(field);

        public static bool Accepts(DeviceType type, DeviceField field) => Contains(CommandFields, type, field);

        public static bool AcceptsReport(DeviceType type, DeviceField field) => Contains(ReportFields, type, field);

        /// <summary>The TV and phone are watched, never commanded.</summary>
        public static bool IsReadOnly(DeviceType type) => !CommandFields.ContainsKey(type);

        /// <summary>Returns a copy with every field the type does not support set to null.</summary>
        public static DeviceState Normalize(DeviceType type, DeviceState state)
        {
            var copy = (state ?? new DeviceState()).Clone();
            var supported = StateFields[type];
            if (!supported.Contains(DeviceField.On)) copy.On = null;
            if (!supported.Contains(DeviceField.Brightness)) copy.Brightness = null;
            if (!supported.Contains(DeviceField.ColorTempK)) copy.ColorTempK = null;
            if (!supported.Contains(DeviceField.Home)) copy.Home = null;
            if (!supported.Contains(DeviceField.Charging)) copy.Charging = null;
            if (!supported.Contains(DeviceField.Asleep)) copy.Asleep = null;
            return copy;
        }

        /// <summary>Reads a boolean field for trigger/condition matching (null when absent or not a boolean field).</summary>
        public static bool? BooleanValue(DeviceState state, DeviceField field)
        {
            switch (field)
            {
                case DeviceField.On:
                    return state.On;
                case DeviceField.Home:
                    return state.Home;
                case DeviceField.Charging:
                    return state.Charging;
                case DeviceField.Asleep:
                    return state.Asleep;
                default:
                    return null;
            }
        }

        public static bool IsBooleanField(DeviceField field) =>
            field == DeviceField.On || field == DeviceField.Home || field == DeviceField.Charging || field == DeviceField.Asleep;

        private static bool Contains(Dictionary<DeviceType, DeviceField[]> map, DeviceType type, DeviceField field)
        {
            DeviceField[] fields;
            return map.TryGetValue(type, out fields) && fields.Contains(field);
        }
    }
}
