using System;
using Homebase.Domain;
using Homebase.Domain.Automations;
using Homebase.Domain.Devices;

namespace Homebase.Data
{
    /// <summary>
    /// Codes stored in CHECK-constrained columns. Kept separate from the API's code maps on purpose: the database and
    /// the HTTP contract may evolve independently (today the spellings happen to match).
    /// </summary>
    internal static class DbCodes
    {
        public static readonly CodeMap<DeviceType> DeviceType = new CodeMap<DeviceType>()
            .Add(Domain.Devices.DeviceType.Light, "light").Add(Domain.Devices.DeviceType.Tv, "tv").Add(Domain.Devices.DeviceType.Phone, "phone");

        public static readonly CodeMap<DeviceField> Field = new CodeMap<DeviceField>()
            .Add(DeviceField.On, "on").Add(DeviceField.Home, "home").Add(DeviceField.Charging, "charging").Add(DeviceField.Asleep, "asleep");

        public static readonly CodeMap<ActionKind> ActionKind = new CodeMap<ActionKind>()
            .Add(Domain.Automations.ActionKind.Device, "device").Add(Domain.Automations.ActionKind.Room, "room").Add(Domain.Automations.ActionKind.Scene, "scene");

        public static readonly CodeMap<TriggerKind> TriggerKind = new CodeMap<TriggerKind>()
            .Add(Domain.Automations.TriggerKind.Device, "device").Add(Domain.Automations.TriggerKind.Time, "time");

        public static readonly CodeMap<ConditionKind> ConditionKind = new CodeMap<ConditionKind>()
            .Add(Domain.Automations.ConditionKind.TimeWindow, "time-window").Add(Domain.Automations.ConditionKind.Device, "device")
            .Add(Domain.Automations.ConditionKind.Dark, "dark");

        public static readonly CodeMap<RunOutcome> Outcome = new CodeMap<RunOutcome>()
            .Add(RunOutcome.Executed, "executed").Add(RunOutcome.Skipped, "skipped").Add(RunOutcome.Failed, "failed").Add(RunOutcome.Cancelled, "cancelled");

        public static readonly CodeMap<DayOfWeek> Weekday = new CodeMap<DayOfWeek>()
            .Add(DayOfWeek.Monday, "mon").Add(DayOfWeek.Tuesday, "tue").Add(DayOfWeek.Wednesday, "wed").Add(DayOfWeek.Thursday, "thu")
            .Add(DayOfWeek.Friday, "fri").Add(DayOfWeek.Saturday, "sat").Add(DayOfWeek.Sunday, "sun");

        /// <summary>An unknown code in a CHECK-constrained column means the schema and the code disagree: fail loudly.</summary>
        public static T Read<T>(CodeMap<T> map, string code)
            where T : struct
        {
            T value;
            if (!map.TryParse(code, out value))
            {
                throw new InvalidOperationException("Unknown " + typeof(T).Name + " code '" + code + "' in database.");
            }

            return value;
        }

        public static T? ReadOrNull<T>(CodeMap<T> map, string code)
            where T : struct => code == null ? (T?)null : Read(map, code);
    }
}
