using System;
using Homebase.Domain;
using Homebase.Domain.Automations;
using Homebase.Domain.Devices;

namespace Homebase.Api.Mapping
{
    /// <summary>Enum values exactly as spelled in contracts/openapi.yaml.</summary>
    public static class ApiCodes
    {
        public static readonly CodeMap<DeviceType> DeviceType = new CodeMap<DeviceType>()
            .Add(Domain.Devices.DeviceType.Light, "light")
            .Add(Domain.Devices.DeviceType.Tv, "tv")
            .Add(Domain.Devices.DeviceType.Phone, "phone");

        /// <summary>Only the fields triggers and conditions may watch.</summary>
        public static readonly CodeMap<DeviceField> Field = new CodeMap<DeviceField>()
            .Add(DeviceField.On, "on")
            .Add(DeviceField.Home, "home")
            .Add(DeviceField.Charging, "charging")
            .Add(DeviceField.Asleep, "asleep");

        public static readonly CodeMap<ActionKind> ActionKind = new CodeMap<ActionKind>()
            .Add(Domain.Automations.ActionKind.Device, "device")
            .Add(Domain.Automations.ActionKind.Room, "room")
            .Add(Domain.Automations.ActionKind.Scene, "scene");

        public static readonly CodeMap<TriggerKind> TriggerKind = new CodeMap<TriggerKind>()
            .Add(Domain.Automations.TriggerKind.Device, "device")
            .Add(Domain.Automations.TriggerKind.Time, "time");

        public static readonly CodeMap<ConditionKind> ConditionKind = new CodeMap<ConditionKind>()
            .Add(Domain.Automations.ConditionKind.TimeWindow, "time-window")
            .Add(Domain.Automations.ConditionKind.Device, "device")
            .Add(Domain.Automations.ConditionKind.Dark, "dark");

        public static readonly CodeMap<RunOutcome> Outcome = new CodeMap<RunOutcome>()
            .Add(RunOutcome.Executed, "executed")
            .Add(RunOutcome.Skipped, "skipped")
            .Add(RunOutcome.Failed, "failed")
            .Add(RunOutcome.Cancelled, "cancelled");

        public static readonly CodeMap<DayOfWeek> Weekday = new CodeMap<DayOfWeek>()
            .Add(DayOfWeek.Monday, "mon")
            .Add(DayOfWeek.Tuesday, "tue")
            .Add(DayOfWeek.Wednesday, "wed")
            .Add(DayOfWeek.Thursday, "thu")
            .Add(DayOfWeek.Friday, "fri")
            .Add(DayOfWeek.Saturday, "sat")
            .Add(DayOfWeek.Sunday, "sun");
    }
}
