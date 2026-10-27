using System.Linq;
using Homebase.Api.Contracts;
using Homebase.Application;
using Homebase.Domain.Automations;
using Homebase.Domain.Devices;

namespace Homebase.Api.Mapping
{
    /// <summary>Domain ↔ wire shapes. No rules here: summaries come from Describe, decisions from the domain.</summary>
    public static class ApiMapper
    {
        public static RoomDto ToDto(Room room) => new RoomDto { Id = room.Id, Name = room.Name };

        public static DeviceDto ToDto(Device device) => new DeviceDto
        {
            Id = device.Id,
            Name = device.Name,
            RoomId = device.RoomId,
            Type = ApiCodes.DeviceType.Code(device.Type),
            Driver = device.Driver,
            Online = device.Online,
            NeedsPairing = device.NeedsPairing,
            LastChangedAt = device.LastChangedAt,
            State = new DeviceStateDto
            {
                On = device.State.On,
                Brightness = device.State.Brightness,
                ColorTempK = device.State.ColorTempK,
                Home = device.State.Home,
                Charging = device.State.Charging,
                Asleep = device.State.Asleep,
            },
        };

        public static DeviceCommand ToDomain(DeviceCommandDto dto) =>
            dto == null ? null : new DeviceCommand { On = dto.On, Brightness = dto.Brightness, ColorTempK = dto.ColorTempK };

        public static DeviceReport ToDomain(DeviceReportDto dto) =>
            dto == null ? null : new DeviceReport { Home = dto.Home, Charging = dto.Charging, Asleep = dto.Asleep };

        public static DiscoveryDto ToDto(DiscoveryOutcome outcome) => new DiscoveryDto
        {
            Added = outcome.Added.Select(ToDto).ToList(),
            Updated = outcome.Updated.Select(ToDto).ToList(),
            Notes = outcome.Notes.ToList(),
        };

        public static PresenceDto ToDto(Presence presence, string reportToken) => new PresenceDto
        {
            Home = presence.Home,
            PhoneDeviceId = presence.PhoneDeviceId,
            TvDeviceId = presence.TvDeviceId,
            ReportToken = reportToken,
            Sun = new SunDto
            {
                Dark = presence.Sun.Dark,
                Sunrise = presence.Sun.Sunrise.HasValue ? presence.Sun.Sunrise.Value.ToString() : null,
                Sunset = presence.Sun.Sunset.HasValue ? presence.Sun.Sunset.Value.ToString() : null,
                LocationSet = presence.Sun.LocationSet,
            },
        };

        public static ActionDto ToDto(HomeAction action, NameLookup names) => new ActionDto
        {
            Kind = ApiCodes.ActionKind.Code(action.Kind),
            DeviceId = action.DeviceId,
            RoomId = action.RoomId,
            SceneId = action.SceneId,
            On = action.Command == null ? null : action.Command.On,
            Brightness = action.Command == null ? null : action.Command.Brightness,
            ColorTempK = action.Command == null ? null : action.Command.ColorTempK,
            Summary = Describe.Action(action, names),
        };

        public static SceneDto ToDto(Scene scene, NameLookup names) => new SceneDto
        {
            Id = scene.Id,
            Name = scene.Name,
            Description = scene.Description,
            Actions = scene.Actions.Select(a => ToDto(a, names)).ToList(),
        };

        public static SceneActivationDto ToDto(SceneActivation activation, NameLookup names) => new SceneActivationDto
        {
            SceneId = activation.SceneId,
            Results = activation.Results.Select(r => new ActionResultDto { Action = ToDto(r.Action, names), Ok = r.Ok, ErrorCode = r.Ok ? null : r.ErrorCode }).ToList(),
        };

        public static AutomationDto ToDto(Automation automation, NameLookup names) => new AutomationDto
        {
            Id = automation.Id,
            Name = automation.Name,
            Description = automation.Description,
            Enabled = automation.Enabled,
            Trigger = new TriggerDto
            {
                Kind = ApiCodes.TriggerKind.Code(automation.Trigger.Kind),
                DeviceId = automation.Trigger.DeviceId,
                Field = ApiCodes.Field.CodeOrNull(automation.Trigger.Field),
                EqualsValue = automation.Trigger.EqualsValue,
                ForMinutes = automation.Trigger.ForMinutes,
                At = automation.Trigger.At,
                Days = automation.Trigger.Days == null ? null : automation.Trigger.Days.Select(ApiCodes.Weekday.Code).ToList(),
                Summary = Describe.Trigger(automation.Trigger, names),
            },
            Conditions = automation.Conditions.Select(c => new ConditionDto
            {
                Kind = ApiCodes.ConditionKind.Code(c.Kind),
                After = c.After,
                Before = c.Before,
                DeviceId = c.DeviceId,
                Field = ApiCodes.Field.CodeOrNull(c.Field),
                EqualsValue = c.EqualsValue,
                Summary = Describe.Condition(c, names),
            }).ToList(),
            Actions = automation.Actions.Select(a => ToDto(a, names)).ToList(),
            LastRunAt = automation.LastRunAt,
            LastOutcome = ApiCodes.Outcome.CodeOrNull(automation.LastOutcome),
            WaitingUntil = automation.WaitingUntil,
        };

        public static SimulationDto ToDto(Simulation simulation, NameLookup names) => new SimulationDto
        {
            AutomationId = simulation.AutomationId,
            WouldRun = simulation.Evaluation.WouldRun,
            EvaluatedAt = simulation.EvaluatedAt,
            Trace = simulation.Evaluation.Trace.Select(ToDto).ToList(),
            PlannedActions = simulation.Evaluation.Planned.Select(a => ToDto(a, names)).ToList(),
        };

        public static AutomationRunDto ToDto(AutomationRun run) => new AutomationRunDto
        {
            Id = run.Id,
            AutomationId = run.AutomationId,
            AutomationName = run.AutomationName,
            FiredAt = run.FiredAt,
            Outcome = ApiCodes.Outcome.Code(run.Outcome),
            Trace = run.Trace.Select(ToDto).ToList(),
            ActionsExecuted = run.ActionsExecuted,
        };

        private static TraceStepDto ToDto(TraceStep step) => new TraceStepDto { Label = step.Label, Passed = step.Passed, Detail = step.Detail };
    }
}
