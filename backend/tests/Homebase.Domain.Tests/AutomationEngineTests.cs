using System;
using System.Linq;
using Homebase.Domain.Automations;
using Homebase.Domain.Devices;
using Xunit;
using static Homebase.Domain.Tests.TestData;

namespace Homebase.Domain.Tests
{
    public class AutomationEngineTests
    {
        private static Automation LeaveAllOff() => new Automation
        {
            Id = "leave-all-off",
            Name = "Leaving home → all off",
            Enabled = true,
            Trigger = new Trigger { Kind = TriggerKind.Device, DeviceId = "phone", Field = DeviceField.Home, EqualsValue = false },
            Actions = { HomeAction.ForScene("all-off") },
        };

        private static Automation ArriveAfterDark() => new Automation
        {
            Id = "arrive-after-dark",
            Name = "Home after dark",
            Enabled = true,
            Trigger = new Trigger { Kind = TriggerKind.Device, DeviceId = "phone", Field = DeviceField.Home, EqualsValue = true },
            Conditions = { new Condition { Kind = ConditionKind.Dark, EqualsValue = true } },
            Actions = { HomeAction.ForRoom("living-room", new DeviceCommand { Brightness = 70, ColorTempK = 2700 }) },
        };

        private static Automation ChargingAtNight() => new Automation
        {
            Id = "charging-at-night",
            Name = "Phone charging at night",
            Enabled = true,
            Trigger = new Trigger { Kind = TriggerKind.Device, DeviceId = "phone", Field = DeviceField.Charging, EqualsValue = true, ForMinutes = 15 },
            Conditions = { new Condition { Kind = ConditionKind.TimeWindow, After = "21:00", Before = "05:00" } },
            Actions = { HomeAction.ForRoom("bedroom", new DeviceCommand { On = false }) },
        };

        private static Automation Timed(string at, params DayOfWeek[] days) => new Automation
        {
            Id = "timed",
            Name = "Timed",
            Enabled = true,
            Trigger = new Trigger { Kind = TriggerKind.Time, At = at, Days = days.Length == 0 ? null : days.ToList() },
            Actions = { HomeAction.ForDevice("light-bedside", new DeviceCommand { On = true }) },
        };

        [Fact]
        public void DeviceTrigger_FiresOnTransitionIntoMatch()
        {
            var phone = Phone(home: true);
            var change = Change(phone, s => s.Home = false);
            var result = AutomationEngine.Evaluate(LeaveAllOff(), Context(EvaluationReason.DeviceChange, Now, new[] { phone }, change));

            Assert.True(result.Triggered);
            Assert.True(result.WouldRun);
            Assert.Single(result.Planned);
            Assert.Equal("Phone home changed from true to false.", result.Trace[0].Detail);
        }

        [Fact]
        public void DeviceTrigger_FiresOnFirstReportFromUnknown()
        {
            var phone = Phone(home: null);
            var change = Change(phone, s => s.Home = false);
            Assert.True(AutomationEngine.Evaluate(LeaveAllOff(), Context(EvaluationReason.DeviceChange, Now, new[] { phone }, change)).Triggered);
        }

        [Fact]
        public void DeviceTrigger_DoesNotFireWhenItAlreadyMatched()
        {
            var phone = Phone(home: false, charging: false);
            var change = Change(phone, s => s.Charging = true);
            var result = AutomationEngine.Evaluate(LeaveAllOff(), Context(EvaluationReason.DeviceChange, Now, new[] { phone }, change));

            Assert.False(result.Triggered);
            Assert.Contains("already matched", result.Trace.Last().Detail);
        }

        [Fact]
        public void DeviceTrigger_IgnoresOtherDevices()
        {
            var tv = Tv(false);
            var change = Change(tv, s => s.On = true);
            var result = AutomationEngine.Evaluate(LeaveAllOff(), Context(EvaluationReason.DeviceChange, Now, new[] { tv, Phone() }, change));

            Assert.False(result.Triggered);
            Assert.Equal("Not triggered by this change.", result.Trace[0].Detail);
        }

        [Fact]
        public void ForMinutes_DefersInsteadOfFiring()
        {
            var phone = Phone(charging: false);
            var change = Change(phone, s => s.Charging = true);
            var result = AutomationEngine.Evaluate(ChargingAtNight(), Context(EvaluationReason.DeviceChange, Now, new[] { phone }, change));

            Assert.False(result.Triggered);
            Assert.True(result.Deferred);
            Assert.Empty(result.Planned);
            Assert.Contains("Waiting 15 min", result.Trace[0].Detail);
        }

        [Fact]
        public void HeldFor_FiresWhenTheFieldStillMatches()
        {
            var phone = Phone(charging: true);
            var pending = new PendingTrigger { AutomationId = "charging-at-night", DeviceId = "phone", Since = Now.AddMinutes(-15), DueAt = Now };
            var result = AutomationEngine.Evaluate(ChargingAtNight(), Context(EvaluationReason.HeldFor, Now, new[] { phone }, pending: pending));

            Assert.True(result.Triggered);
            Assert.True(result.WouldRun);
            Assert.Equal("Phone has been charging since 22:57 (15 min).", result.Trace[0].Detail);
        }

        [Fact]
        public void HeldFor_DoesNotFireWhenTheFieldNoLongerMatches()
        {
            var phone = Phone(charging: false);
            var pending = new PendingTrigger { AutomationId = "charging-at-night", DeviceId = "phone", Since = Now.AddMinutes(-15), DueAt = Now };
            var result = AutomationEngine.Evaluate(ChargingAtNight(), Context(EvaluationReason.HeldFor, Now, new[] { phone }, pending: pending));

            Assert.False(result.Triggered);
            Assert.Equal("Phone is no longer charging (charging is false).", result.Trace[0].Detail);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public void DarkCondition_FollowsTheSun(bool dark, bool wouldRun)
        {
            var phone = Phone(home: false);
            var change = Change(phone, s => s.Home = true);
            var result = AutomationEngine.Evaluate(ArriveAfterDark(), Context(EvaluationReason.DeviceChange, Now, new[] { phone }, change, SunAt(dark)));

            Assert.True(result.Triggered);
            Assert.Equal(wouldRun, result.WouldRun);
            Assert.Equal("Condition: It is dark", result.Trace[1].Label);
            Assert.Equal("Sunrise 07:12, sunset 17:48; now is 23:12 (household time).", result.Trace[1].Detail);
        }

        [Fact]
        public void DarkCondition_ExplainsTheFallbackWithoutALocation()
        {
            var phone = Phone(home: false);
            var change = Change(phone, s => s.Home = true);
            var sun = new Sun.SunInfo { Dark = true, LocationSet = false };
            var result = AutomationEngine.Evaluate(ArriveAfterDark(), Context(EvaluationReason.DeviceChange, Now, new[] { phone }, change, sun));

            Assert.True(result.WouldRun);
            Assert.Contains("No location set", result.Trace[1].Detail);
            Assert.Contains("18:00–07:00", result.Trace[1].Detail);
        }

        [Theory]
        [InlineData("22:00", "06:30", 23, 12, true)]
        [InlineData("22:00", "06:30", 6, 29, true)]
        [InlineData("22:00", "06:30", 6, 30, false)]
        [InlineData("22:00", "06:30", 21, 59, false)]
        [InlineData("08:00", "18:00", 8, 0, true)]
        [InlineData("08:00", "18:00", 18, 0, false)]
        public void TimeWindow_IsInclusiveExclusiveAndWrapsMidnight(string after, string before, int hour, int minute, bool expected)
        {
            Assert.Equal(expected, AutomationEngine.InTimeWindow(ClockTime.FromHourMinute(hour, minute), ClockTime.Parse(after), ClockTime.Parse(before)));
        }

        [Fact]
        public void Simulation_AssumesTheTriggerAndExplainsHeldFor()
        {
            var result = AutomationEngine.Evaluate(ChargingAtNight(), Context(EvaluationReason.Simulation, Now, new[] { Phone() }));

            Assert.True(result.Triggered);
            Assert.True(result.WouldRun);
            Assert.Contains("held for 15 min", result.Trace[0].Detail);
        }

        [Fact]
        public void DisabledAutomation_DoesNotRunButCanBeSimulated()
        {
            var automation = LeaveAllOff();
            automation.Enabled = false;
            var phone = Phone(home: true);
            var change = Change(phone, s => s.Home = false);

            Assert.False(AutomationEngine.Evaluate(automation, Context(EvaluationReason.DeviceChange, Now, new[] { phone }, change)).Triggered);
            var simulated = AutomationEngine.Evaluate(automation, Context(EvaluationReason.Simulation, Now, new[] { phone }));
            Assert.True(simulated.Triggered);
            Assert.False(simulated.WouldRun);
        }

        [Fact]
        public void LoopGuard_StopsSelfRetriggerAndDeepChains()
        {
            var phone = Phone(home: true);
            var self = Change(phone, s => s.Home = false, AutomationChain.Empty.Extend("leave-all-off"));
            Assert.Contains("Loop guard", AutomationEngine.Evaluate(LeaveAllOff(), Context(EvaluationReason.DeviceChange, Now, new[] { phone }, self)).Trace[0].Detail);

            phone = Phone(home: true);
            var deep = Change(phone, s => s.Home = false, AutomationChain.Empty.Extend("a").Extend("b").Extend("c"));
            Assert.Contains("chain depth 3", AutomationEngine.Evaluate(LeaveAllOff(), Context(EvaluationReason.DeviceChange, Now, new[] { phone }, deep)).Trace[0].Detail);
        }

        [Fact]
        public void TimeTrigger_FiresOncePerMinuteOnTheRightDays()
        {
            var monday = new DateTime(2026, 10, 26, 7, 0, 20, DateTimeKind.Utc);
            Assert.True(AutomationEngine.Evaluate(Timed("07:00"), Context(EvaluationReason.TimeTick, monday, new[] { Light() })).Triggered);
            Assert.False(AutomationEngine.Evaluate(Timed("07:00", DayOfWeek.Saturday), Context(EvaluationReason.TimeTick, monday, new[] { Light() })).Triggered);

            var ran = Timed("07:00");
            ran.LastRunAt = monday.AddSeconds(-15);
            Assert.Contains("Already ran", AutomationEngine.Evaluate(ran, Context(EvaluationReason.TimeTick, monday, new[] { Light() })).Trace[0].Detail);
        }

        [Theory]
        [InlineData(RunOutcome.Skipped, false, 0)]
        [InlineData(RunOutcome.Failed, true, 1)]
        [InlineData(RunOutcome.Executed, true, 0)]
        public void Outcome(RunOutcome expected, bool wouldRun, int failed)
        {
            Assert.Equal(expected, AutomationEngine.DecideOutcome(wouldRun, failed));
        }
    }
}
