using System;
using System.Linq;
using System.Threading.Tasks;
using Homebase.Domain;
using Homebase.Domain.Automations;
using Homebase.Domain.Devices;
using Homebase.Domain.Sun;
using Xunit;

namespace Homebase.Application.Tests
{
    public class PresenceFlowTests
    {
        private static async Task LightsOn(HomeFixture f)
        {
            foreach (var id in new[] { "light-floor-lamp", "light-tv-strip", "light-bedside" })
            {
                await f.Services.Devices.CommandAsync(id, new DeviceCommand { On = true });
            }
        }

        [Fact]
        public async Task LeavingHome_TurnsEveryLightOff()
        {
            var f = new HomeFixture();
            await LightsOn(f);

            await f.Services.Devices.ReportAsync("phone", new DeviceReport { Home = false });

            Assert.All(f.Home.Devices.Where(d => d.Type == DeviceType.Light), d => Assert.False(d.State.On));
            var run = f.Home.RunsOf("leave-all-off").Single();
            Assert.Equal(RunOutcome.Executed, run.Outcome);
            Assert.Equal("Phone home changed from true to false.", run.Trace[0].Detail);
        }

        [Fact]
        public async Task ArrivingAfterDark_LightsTheLivingRoomOnly()
        {
            var f = new HomeFixture();
            await f.Services.Devices.ReportAsync("phone", new DeviceReport { Home = false });

            await f.Services.Devices.ReportAsync("phone", new DeviceReport { Home = true });

            Assert.Equal(70, f.Home.Device("light-floor-lamp").State.Brightness);
            Assert.Equal(2700, f.Home.Device("light-floor-lamp").State.ColorTempK);
            Assert.True(f.Home.Device("light-tv-strip").State.On);
            Assert.False(f.Home.Device("light-bedside").State.On);
            Assert.Equal(RunOutcome.Executed, f.Home.RunsOf("arrive-after-dark").Single().Outcome);
        }

        [Fact]
        public async Task ArrivingInDaylight_IsSkippedWithTheSunTimes()
        {
            var noonInLondon = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
            var f = new HomeFixture(noonInLondon, new GeoLocation(51.5074, -0.1278));
            await f.Services.Devices.ReportAsync("phone", new DeviceReport { Home = false });

            await f.Services.Devices.ReportAsync("phone", new DeviceReport { Home = true });

            var run = f.Home.RunsOf("arrive-after-dark").Single();
            Assert.Equal(RunOutcome.Skipped, run.Outcome);
            Assert.False(run.Trace[1].Passed);
            Assert.StartsWith("Sunrise 06:", run.Trace[1].Detail);
            Assert.False(f.Home.Device("light-floor-lamp").State.On);
        }

        [Fact]
        public async Task ChargingAtNight_WaitsFifteenMinutesThenTurnsTheBedroomOff()
        {
            var f = new HomeFixture();
            await f.Services.Devices.CommandAsync("light-bedside", new DeviceCommand { On = true });

            await f.Services.Devices.ReportAsync("phone", new DeviceReport { Charging = true });

            Assert.True(f.Home.Device("light-bedside").State.On);
            Assert.Empty(f.Home.RunsOf("charging-at-night"));
            Assert.Equal(f.Clock.UtcNow.AddMinutes(15), (await f.Services.Automations.GetAsync("charging-at-night")).WaitingUntil);

            f.Clock.Advance(TimeSpan.FromMinutes(14));
            await f.Services.Automations.TickAsync();
            Assert.True(f.Home.Device("light-bedside").State.On);

            f.Clock.Advance(TimeSpan.FromMinutes(1));
            await f.Services.Automations.TickAsync();

            Assert.False(f.Home.Device("light-bedside").State.On);
            var run = f.Home.RunsOf("charging-at-night").Single();
            Assert.Equal(RunOutcome.Executed, run.Outcome);
            Assert.Equal("Phone has been charging since 23:12 (15 min).", run.Trace[0].Detail);
            Assert.Null((await f.Services.Automations.GetAsync("charging-at-night")).WaitingUntil);
        }

        [Fact]
        public async Task UnpluggingEarly_CancelsTheCountdownAndSaysWhy()
        {
            var f = new HomeFixture();
            await f.Services.Devices.CommandAsync("light-bedside", new DeviceCommand { On = true });
            await f.Services.Devices.ReportAsync("phone", new DeviceReport { Charging = true });

            f.Clock.Advance(TimeSpan.FromMinutes(6));
            await f.Services.Devices.ReportAsync("phone", new DeviceReport { Charging = false });
            f.Clock.Advance(TimeSpan.FromMinutes(20));
            await f.Services.Automations.TickAsync();

            Assert.True(f.Home.Device("light-bedside").State.On);
            var run = f.Home.RunsOf("charging-at-night").Single();
            Assert.Equal(RunOutcome.Cancelled, run.Outcome);
            Assert.Equal("Phone stopped being charging after 6 of 15 min.", run.Trace.Single().Detail);
        }

        [Fact]
        public async Task DisablingAnAutomation_DropsItsCountdown()
        {
            var f = new HomeFixture();
            await f.Services.Devices.ReportAsync("phone", new DeviceReport { Charging = true });

            var disabled = await f.Services.Automations.SetEnabledAsync("charging-at-night", false);
            f.Clock.Advance(TimeSpan.FromMinutes(30));
            await f.Services.Automations.TickAsync();

            Assert.Null(disabled.WaitingUntil);
            Assert.Empty(f.Home.RunsOf("charging-at-night"));
        }

        [Fact]
        public async Task TvOnAfterDark_ThenOffLate_IsTheWalkToBed()
        {
            var f = new HomeFixture();
            f.Tv.On = true;
            await f.Services.Automations.PollDevicesAsync();

            Assert.Equal(40, f.Home.Device("light-floor-lamp").State.Brightness);
            Assert.True(f.Home.Device("light-floor-lamp").State.On);

            f.Clock.Advance(TimeSpan.FromMinutes(30));
            f.Tv.On = false;
            await f.Services.Automations.PollDevicesAsync();

            Assert.False(f.Home.Device("light-floor-lamp").State.On);
            Assert.Equal(25, f.Home.Device("light-bedside").State.Brightness);
            Assert.Equal(2200, f.Home.Device("light-bedside").State.ColorTempK);
            Assert.Equal(RunOutcome.Executed, f.Home.RunsOf("tv-off-late").Single().Outcome);
        }

        [Fact]
        public async Task PollingRespectsTheDriverInterval_AndAnUnreachableTvGoesOffline()
        {
            var f = new HomeFixture();
            f.Tv.On = true;
            Assert.Equal(1, await f.Services.Automations.PollDevicesAsync());

            f.Tv.On = false;
            Assert.Equal(0, await f.Services.Automations.PollDevicesAsync()); // read again only after 15 s

            f.Clock.Advance(TimeSpan.FromSeconds(15));
            f.Tv.Reachable = false;
            await f.Services.Automations.PollDevicesAsync();

            var tv = f.Home.Device("tv");
            Assert.False(tv.Online);
            Assert.True(tv.State.On);
        }

        [Fact]
        public async Task WentToSleep_AllOff_AndWokeUpOnADarkMorning_BedroomOn()
        {
            var f = new HomeFixture();
            await LightsOn(f);
            await f.Services.Devices.ReportAsync("phone", new DeviceReport { Asleep = true });
            Assert.All(f.Home.Devices.Where(d => d.Type == DeviceType.Light), d => Assert.False(d.State.On));

            f.Clock.UtcNow = new DateTime(2026, 10, 27, 6, 30, 0, DateTimeKind.Utc);
            await f.Services.Devices.ReportAsync("phone", new DeviceReport { Asleep = false });

            Assert.Equal(60, f.Home.Device("light-bedside").State.Brightness);
            Assert.Equal(4000, f.Home.Device("light-bedside").State.ColorTempK);
            Assert.False(f.Home.Device("light-floor-lamp").State.On);
        }

        [Fact]
        public async Task RoomActionWithAnOfflineLight_ChangesTheOthersAndRecordsTheFailure()
        {
            var f = new HomeFixture();
            await LightsOn(f);
            f.Home.Device("light-tv-strip").Online = false;

            await f.Services.Devices.ReportAsync("phone", new DeviceReport { Home = false });

            Assert.False(f.Home.Device("light-floor-lamp").State.On);
            Assert.False(f.Home.Device("light-bedside").State.On);
            var run = f.Home.RunsOf("leave-all-off").Single();
            Assert.Equal(RunOutcome.Failed, run.Outcome);
            Assert.Contains(run.Trace, t => !t.Passed && t.Detail.Contains("TV backlight is offline") && t.Detail.Contains("device_offline"));
        }

        [Fact]
        public async Task OnlyThePhoneCanReport()
        {
            var f = new HomeFixture();
            var ex = await Assert.ThrowsAsync<DomainException>(() => f.Services.Devices.ReportAsync("tv", new DeviceReport { Home = true }));
            Assert.Equal(ErrorCodes.NotReportable, ex.Code);
        }

        [Fact]
        public async Task Presence_ReadsThePhoneAndTheSun()
        {
            var f = new HomeFixture();
            var presence = await f.Services.Presence.StatusAsync();

            Assert.True(presence.Home);
            Assert.Equal("phone", presence.PhoneDeviceId);
            Assert.Equal("tv", presence.TvDeviceId);
            Assert.True(presence.Sun.Dark);
            Assert.False(presence.Sun.LocationSet);
        }

        [Fact]
        public async Task Simulate_ChangesNothing()
        {
            var f = new HomeFixture();
            var simulation = await f.Services.Automations.SimulateAsync("leave-all-off");

            Assert.True(simulation.Evaluation.WouldRun);
            Assert.Single(simulation.Evaluation.Planned);
            Assert.Empty(f.Home.Runs);
        }
    }
}
