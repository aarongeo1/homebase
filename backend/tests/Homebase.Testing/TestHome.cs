using System;
using System.Collections.Generic;
using Homebase.Domain.Automations;
using Homebase.Domain.Devices;

namespace Homebase.Testing
{
    public static class TestHome
    {
        /// <summary>Monday 2026-10-26 23:12 UTC (household zone UTC in tests): late evening, dark without a location.</summary>
        public static readonly DateTime DefaultNow = new DateTime(2026, 10, 26, 23, 12, 0, DateTimeKind.Utc);

        /// <summary>
        /// The household. Rooms, the phone, the TV, scenes and automations mirror database/migrations/0002_seed_household.sql
        /// (same ids, names and rules). Added for tests: three simulated lights, a phone that has reported once (home,
        /// unplugged, awake) and a paired TV that is off.
        /// </summary>
        public static InMemoryHome Create(DateTime nowUtc)
        {
            var home = new InMemoryHome();
            home.Rooms.AddRange(new[]
            {
                new Room { Id = "living-room", Name = "Living room", SortOrder = 1 },
                new Room { Id = "bedroom", Name = "Bedroom", SortOrder = 2 },
            });

            home.Devices.AddRange(new[]
            {
                new Device { Id = "phone", Name = "Phone", Type = DeviceType.Phone, Driver = DriverKeys.Push, Online = true, LastChangedAt = nowUtc, State = new DeviceState { Home = true, Charging = false, Asleep = false } },
                new Device { Id = "tv", Name = "Dining room TV", RoomId = "living-room", Type = DeviceType.Tv, Driver = DriverKeys.AndroidTv, DriverAddress = "192.168.1.40", Online = true, LastChangedAt = nowUtc, State = new DeviceState { On = false } },
                Light("light-floor-lamp", "Floor lamp", "living-room", nowUtc),
                Light("light-tv-strip", "TV backlight", "living-room", nowUtc),
                Light("light-bedside", "Bedside lamp", "bedroom", nowUtc),
            });

            home.Scenes.AddRange(new[]
            {
                new Scene { Id = "all-off", Name = "All off", Description = "Every light in the living room and bedroom off.", Actions = { HomeAction.ForRoom("living-room", Off()), HomeAction.ForRoom("bedroom", Off()) } },
                new Scene { Id = "evening", Name = "Evening", Description = "Living room warm and fairly bright.", Actions = { HomeAction.ForRoom("living-room", Warm(70, 2700)) } },
                new Scene { Id = "bedtime", Name = "Bedtime", Description = "Living room off; bedroom warm and dim.", Actions = { HomeAction.ForRoom("living-room", Off()), HomeAction.ForRoom("bedroom", Warm(25, 2200)) } },
            });

            home.Automations.AddRange(new[]
            {
                OnDevice("leave-all-off", "Leaving home → all off", "phone", DeviceField.Home, false, null, null, HomeAction.ForScene("all-off")),
                OnDevice("arrive-after-dark", "Home after dark → living room on", "phone", DeviceField.Home, true, null, new[] { Dark() }, HomeAction.ForRoom("living-room", Warm(70, 2700))),
                OnDevice("tv-on-after-dark", "TV on after dark → living room on", "tv", DeviceField.On, true, null, new[] { Dark() }, HomeAction.ForRoom("living-room", Warm(40, 2700))),
                OnDevice("tv-off-late", "TV off late → bedtime", "tv", DeviceField.On, false, null, new[] { Window("22:00", "03:00") }, HomeAction.ForScene("bedtime")),
                OnDevice("charging-at-night", "Phone charging at night → bedroom off", "phone", DeviceField.Charging, true, 15, new[] { Window("21:00", "05:00") }, HomeAction.ForRoom("bedroom", Off())),
                OnDevice("woke-up", "Woke up → bedroom on", "phone", DeviceField.Asleep, false, null, new[] { Window("05:00", "11:00"), Dark() }, HomeAction.ForRoom("bedroom", Warm(60, 4000))),
                OnDevice("went-to-sleep", "Went to sleep → all off", "phone", DeviceField.Asleep, true, null, null, HomeAction.ForScene("all-off")),
            });

            return home;
        }

        private static Device Light(string id, string name, string roomId, DateTime nowUtc) => new Device
        {
            Id = id,
            Name = name,
            RoomId = roomId,
            Type = DeviceType.Light,
            Driver = DriverKeys.Simulated,
            Online = true,
            LastChangedAt = nowUtc,
            State = new DeviceState { On = false, Brightness = 80, ColorTempK = 3000 },
        };

        private static DeviceCommand Off() => new DeviceCommand { On = false };

        private static DeviceCommand Warm(int brightness, int kelvin) => new DeviceCommand { Brightness = brightness, ColorTempK = kelvin };

        private static Condition Dark() => new Condition { Kind = ConditionKind.Dark, EqualsValue = true };

        private static Condition Window(string after, string before) => new Condition { Kind = ConditionKind.TimeWindow, After = after, Before = before };

        private static Automation OnDevice(string id, string name, string deviceId, DeviceField field, bool equals, int? forMinutes, IEnumerable<Condition> conditions, HomeAction action)
        {
            var automation = new Automation
            {
                Id = id,
                Name = name,
                Description = name,
                Enabled = true,
                Trigger = new Trigger { Kind = TriggerKind.Device, DeviceId = deviceId, Field = field, EqualsValue = equals, ForMinutes = forMinutes },
                Actions = { action },
            };
            if (conditions != null) automation.Conditions.AddRange(conditions);
            return automation;
        }
    }
}
