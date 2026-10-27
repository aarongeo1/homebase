using System;
using System.Collections.Generic;
using System.Linq;
using Homebase.Domain.Devices;

namespace Homebase.Domain.Automations
{
    public enum ActionKind
    {
        /// <summary>One light.</summary>
        Device,

        /// <summary>Every light in a room.</summary>
        Room,

        Scene,
    }

    /// <summary>One step of a scene or automation.</summary>
    public sealed class HomeAction
    {
        public ActionKind Kind { get; set; }

        public string DeviceId { get; set; }

        public string RoomId { get; set; }

        public string SceneId { get; set; }

        public DeviceCommand Command { get; set; }

        public static HomeAction ForDevice(string deviceId, DeviceCommand command) =>
            new HomeAction { Kind = ActionKind.Device, DeviceId = deviceId, Command = command };

        public static HomeAction ForRoom(string roomId, DeviceCommand command) =>
            new HomeAction { Kind = ActionKind.Room, RoomId = roomId, Command = command };

        public static HomeAction ForScene(string sceneId) => new HomeAction { Kind = ActionKind.Scene, SceneId = sceneId };
    }

    public enum TriggerKind
    {
        Device,
        Time,
    }

    public sealed class Trigger
    {
        public const int MaxForMinutes = 240;

        public TriggerKind Kind { get; set; }

        public string DeviceId { get; set; }

        /// <summary>One of On, Home, Charging, Asleep.</summary>
        public DeviceField? Field { get; set; }

        public bool? EqualsValue { get; set; }

        /// <summary>Device triggers only: fire once the field has matched for this long, not the moment it changes.</summary>
        public int? ForMinutes { get; set; }

        /// <summary>Household-local "HH:mm" (time triggers).</summary>
        public string At { get; set; }

        /// <summary>Null means every day.</summary>
        public List<DayOfWeek> Days { get; set; }
    }

    public enum ConditionKind
    {
        TimeWindow,
        Device,

        /// <summary>Passes when darkness matches EqualsValue (true = between sunset and sunrise).</summary>
        Dark,
    }

    public sealed class Condition
    {
        public ConditionKind Kind { get; set; }

        /// <summary>Time window start, household-local "HH:mm", inclusive.</summary>
        public string After { get; set; }

        /// <summary>Time window end, household-local "HH:mm", exclusive. May be earlier than After (wraps midnight).</summary>
        public string Before { get; set; }

        public string DeviceId { get; set; }

        public DeviceField? Field { get; set; }

        public bool? EqualsValue { get; set; }
    }

    public sealed class Automation
    {
        public Automation()
        {
            Conditions = new List<Condition>();
            Actions = new List<HomeAction>();
        }

        public string Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public bool Enabled { get; set; }

        public Trigger Trigger { get; set; }

        public List<Condition> Conditions { get; set; }

        public List<HomeAction> Actions { get; set; }

        public DateTime? LastRunAt { get; set; }

        public RunOutcome? LastOutcome { get; set; }

        /// <summary>Not stored: set while a forMinutes trigger is counting down (lost on restart).</summary>
        public DateTime? WaitingUntil { get; set; }
    }

    public sealed class Scene
    {
        public Scene()
        {
            Actions = new List<HomeAction>();
        }

        public string Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public List<HomeAction> Actions { get; set; }
    }

    public enum RunOutcome
    {
        Executed,

        /// <summary>The trigger fired but a condition failed.</summary>
        Skipped,

        /// <summary>At least one action failed (others still ran).</summary>
        Failed,

        /// <summary>A forMinutes trigger stopped matching before the time was up.</summary>
        Cancelled,
    }

    public sealed class TraceStep
    {
        public TraceStep(string label, bool passed, string detail)
        {
            Label = label;
            Passed = passed;
            Detail = detail;
        }

        public string Label { get; }

        public bool Passed { get; }

        public string Detail { get; }
    }

    public sealed class AutomationRun
    {
        public AutomationRun()
        {
            Trace = new List<TraceStep>();
        }

        public string Id { get; set; }

        public string AutomationId { get; set; }

        public string AutomationName { get; set; }

        public DateTime FiredAt { get; set; }

        public RunOutcome Outcome { get; set; }

        public List<TraceStep> Trace { get; set; }

        public int ActionsExecuted { get; set; }
    }

    /// <summary>
    /// The automations that caused the current change, oldest first. Used by the loop guard: max depth 3, and an
    /// automation never re-triggers within its own chain.
    /// </summary>
    public sealed class AutomationChain
    {
        public const int MaxDepth = 3;

        public static readonly AutomationChain Empty = new AutomationChain(new string[0]);

        private readonly string[] _ids;

        private AutomationChain(string[] ids)
        {
            _ids = ids;
        }

        public IReadOnlyList<string> Ids => _ids;

        public int Depth => _ids.Length;

        public bool Contains(string automationId) => _ids.Contains(automationId, StringComparer.Ordinal);

        public AutomationChain Extend(string automationId) => new AutomationChain(_ids.Concat(new[] { automationId }).ToArray());

        public override string ToString() => _ids.Length == 0 ? "(none)" : string.Join(" → ", _ids);
    }

    /// <summary>A device whose trigger-relevant state changed (on, home, charging, asleep).</summary>
    public sealed class DeviceChange
    {
        private static readonly DeviceField[] TriggerFields = { DeviceField.On, DeviceField.Home, DeviceField.Charging, DeviceField.Asleep };

        public DeviceChange(Device device, DeviceState previous, AutomationChain chain)
        {
            Device = device;
            Previous = previous;
            Chain = chain ?? AutomationChain.Empty;
        }

        public Device Device { get; }

        public DeviceState Previous { get; }

        public DeviceState Current => Device.State;

        public AutomationChain Chain { get; }

        /// <summary>Null when no trigger-relevant field changed.</summary>
        public static DeviceChange Detect(Device device, DeviceState previous, AutomationChain chain)
        {
            foreach (var field in TriggerFields)
            {
                if (DeviceCapabilities.BooleanValue(previous, field) != DeviceCapabilities.BooleanValue(device.State, field))
                {
                    return new DeviceChange(device, previous, chain);
                }
            }

            return null;
        }
    }

    /// <summary>A forMinutes trigger that matched at <see cref="Since"/> and fires at <see cref="DueAt"/> if it still matches.</summary>
    public sealed class PendingTrigger
    {
        public string AutomationId { get; set; }

        public string DeviceId { get; set; }

        public DateTime Since { get; set; }

        public DateTime DueAt { get; set; }

        public AutomationChain Chain { get; set; }
    }
}
