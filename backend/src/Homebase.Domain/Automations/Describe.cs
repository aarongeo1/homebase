using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Homebase.Domain.Devices;

namespace Homebase.Domain.Automations
{
    /// <summary>Resolves ids to display names for summaries; unknown ids fall back to the id itself.</summary>
    public sealed class NameLookup
    {
        private readonly IReadOnlyDictionary<string, string> _devices;
        private readonly IReadOnlyDictionary<string, string> _scenes;
        private readonly IReadOnlyDictionary<string, string> _rooms;

        public NameLookup(IReadOnlyDictionary<string, string> devices, IReadOnlyDictionary<string, string> scenes, IReadOnlyDictionary<string, string> rooms)
        {
            _devices = devices ?? new Dictionary<string, string>();
            _scenes = scenes ?? new Dictionary<string, string>();
            _rooms = rooms ?? new Dictionary<string, string>();
        }

        public static NameLookup From(IEnumerable<Device> devices, IEnumerable<Scene> scenes, IEnumerable<Room> rooms) =>
            new NameLookup(
                devices.ToDictionary(d => d.Id, d => d.Name, StringComparer.Ordinal),
                scenes.ToDictionary(s => s.Id, s => s.Name, StringComparer.Ordinal),
                rooms.ToDictionary(r => r.Id, r => r.Name, StringComparer.Ordinal));

        public string Device(string id) => Lookup(_devices, id);

        public string Scene(string id) => Lookup(_scenes, id);

        public string Room(string id) => Lookup(_rooms, id);

        private static string Lookup(IReadOnlyDictionary<string, string> map, string id)
        {
            string name;
            return id != null && map.TryGetValue(id, out name) ? name : id;
        }
    }

    /// <summary>Server-written plain-English summaries shown in the UI and in traces (e.g. "Bedroom lights → on at 25%, 2200 K").</summary>
    public static class Describe
    {
        private static readonly DayOfWeek[] Weekdays = { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        private static readonly DayOfWeek[] Weekend = { DayOfWeek.Saturday, DayOfWeek.Sunday };

        public static string Action(HomeAction action, NameLookup names)
        {
            switch (action.Kind)
            {
                case ActionKind.Scene:
                    return "Activate scene " + names.Scene(action.SceneId);
                case ActionKind.Room:
                    return names.Room(action.RoomId) + " lights → " + CommandPhrase(action.Command);
                default:
                    return names.Device(action.DeviceId) + " → " + CommandPhrase(action.Command);
            }
        }

        public static string CommandPhrase(DeviceCommand command)
        {
            if (command == null)
            {
                return "(no change)";
            }

            var parts = new List<string>();
            if (command.Brightness.HasValue)
            {
                parts.Add("on at " + command.Brightness.Value.ToString(CultureInfo.InvariantCulture) + "%");
            }
            else if (command.On.HasValue)
            {
                parts.Add(command.On.Value ? "on" : "off");
            }

            if (command.ColorTempK.HasValue) parts.Add(command.ColorTempK.Value.ToString(CultureInfo.InvariantCulture) + " K");
            return parts.Count == 0 ? "(no change)" : string.Join(", ", parts);
        }

        public static string Trigger(Trigger trigger, NameLookup names)
        {
            if (trigger.Kind == TriggerKind.Time)
            {
                return "At " + trigger.At + " " + Days(trigger.Days);
            }

            var device = names.Device(trigger.DeviceId);
            return trigger.ForMinutes.HasValue
                ? "When " + device + " has been " + StateWord(trigger.Field, trigger.EqualsValue) + " for " + trigger.ForMinutes.Value + " min"
                : "When " + device + " " + Transition(trigger.Field, trigger.EqualsValue);
        }

        public static string Condition(Condition condition, NameLookup names)
        {
            switch (condition.Kind)
            {
                case ConditionKind.TimeWindow:
                    return "Between " + condition.After + " and " + condition.Before;
                case ConditionKind.Dark:
                    return condition.EqualsValue == false ? "It is light" : "It is dark";
                default:
                    return names.Device(condition.DeviceId) + " is " + StateWord(condition.Field, condition.EqualsValue);
            }
        }

        public static string Days(IList<DayOfWeek> days)
        {
            if (days == null || days.Count == 0 || days.Distinct().Count() == 7) return "every day";
            var set = days.Distinct().OrderBy(d => ((int)d + 6) % 7).ToList();
            if (set.SequenceEqual(Weekdays)) return "on weekdays";
            if (set.SequenceEqual(Weekend)) return "on weekends";
            return "on " + string.Join(", ", set.Select(d => d.ToString().Substring(0, 3)));
        }

        public static string FieldValue(DeviceState state, DeviceField field)
        {
            var value = DeviceCapabilities.BooleanValue(state, field);
            return value.HasValue ? (value.Value ? "true" : "false") : "unknown";
        }

        /// <summary>"home"/"away", "charging"/"unplugged", "asleep"/"awake", "on"/"off".</summary>
        public static string StateWord(DeviceField? field, bool? equals)
        {
            var yes = equals != false;
            switch (field)
            {
                case DeviceField.Home:
                    return yes ? "home" : "away";
                case DeviceField.Charging:
                    return yes ? "charging" : "unplugged";
                case DeviceField.Asleep:
                    return yes ? "asleep" : "awake";
                default:
                    return yes ? "on" : "off";
            }
        }

        private static string Transition(DeviceField? field, bool? equals)
        {
            var yes = equals != false;
            switch (field)
            {
                case DeviceField.Home:
                    return yes ? "arrives home" : "leaves home";
                case DeviceField.Charging:
                    return yes ? "starts charging" : "stops charging";
                case DeviceField.Asleep:
                    return yes ? "goes to sleep" : "wakes up";
                default:
                    return yes ? "turns on" : "turns off";
            }
        }
    }
}
