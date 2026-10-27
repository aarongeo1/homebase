using System;
using System.Collections.Generic;
using System.Linq;
using Homebase.Domain.Automations;
using Homebase.Domain.Devices;
using Homebase.Domain.Sun;

namespace Homebase.Domain.Tests
{
    internal static class TestData
    {
        // Monday 2026-10-26 23:12 UTC; tests use UTC as the household zone unless stated otherwise.
        public static readonly DateTime Now = new DateTime(2026, 10, 26, 23, 12, 0, DateTimeKind.Utc);

        public static Device Light(string id = "light-bedside", string name = "Bedside lamp", bool on = false, int brightness = 80, bool online = true) =>
            new Device { Id = id, Name = name, RoomId = "bedroom", Type = DeviceType.Light, Online = online, State = new DeviceState { On = on, Brightness = brightness, ColorTempK = 3000 } };

        public static Device Phone(bool? home = true, bool? charging = false, bool? asleep = false) =>
            new Device { Id = "phone", Name = "Phone", Type = DeviceType.Phone, Driver = DriverKeys.Push, Online = true, State = new DeviceState { Home = home, Charging = charging, Asleep = asleep } };

        public static Device Tv(bool? on = false) =>
            new Device { Id = "tv", Name = "Dining room TV", RoomId = "living-room", Type = DeviceType.Tv, Driver = DriverKeys.AndroidTv, Online = true, State = new DeviceState { On = on } };

        public static SunInfo SunAt(bool dark) => new SunInfo { Dark = dark, Sunrise = ClockTime.FromHourMinute(7, 12), Sunset = ClockTime.FromHourMinute(17, 48), LocationSet = true };

        public static EvaluationContext Context(EvaluationReason reason, DateTime nowUtc, IEnumerable<Device> devices, DeviceChange change = null, SunInfo sun = null, PendingTrigger pending = null)
        {
            var list = devices.ToList();
            return new EvaluationContext
            {
                NowUtc = nowUtc,
                TimeZone = TimeZoneInfo.Utc,
                Devices = list.ToDictionary(d => d.Id),
                Reason = reason,
                Change = change,
                Pending = pending,
                Sun = sun ?? SunAt(true),
                Names = NameLookup.From(list, new Scene[0], new[] { new Room { Id = "bedroom", Name = "Bedroom" }, new Room { Id = "living-room", Name = "Living room" } }),
            };
        }

        public static DeviceChange Change(Device device, Action<DeviceState> mutate, AutomationChain chain = null)
        {
            var previous = device.State.Clone();
            mutate(device.State);
            return new DeviceChange(device, previous, chain ?? AutomationChain.Empty);
        }
    }
}
