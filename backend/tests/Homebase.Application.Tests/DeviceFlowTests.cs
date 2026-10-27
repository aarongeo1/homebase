using System.Linq;
using System.Threading.Tasks;
using Homebase.Devices;
using Homebase.Domain;
using Homebase.Domain.Devices;
using Homebase.Testing;
using Xunit;

namespace Homebase.Application.Tests
{
    public class DeviceFlowTests
    {
        [Fact]
        public async Task Brightness_TurnsALightOn()
        {
            var f = new HomeFixture();
            var lamp = await f.Services.Devices.CommandAsync("light-bedside", new DeviceCommand { Brightness = 30 });

            Assert.True(lamp.State.On);
            Assert.Equal(30, lamp.State.Brightness);
            Assert.Equal(f.Clock.UtcNow, f.Home.Device("light-bedside").LastChangedAt);
        }

        [Fact]
        public async Task TvAndOfflineLights_AreRejected()
        {
            var f = new HomeFixture();
            f.Home.Device("light-bedside").Online = false;

            Assert.Equal(ErrorCodes.ReadOnlyDevice, (await Assert.ThrowsAnyAsync<DomainException>(() => f.Services.Devices.CommandAsync("tv", new DeviceCommand { On = true }))).Code);
            Assert.Equal(ErrorCodes.DeviceOffline, (await Assert.ThrowsAnyAsync<DomainException>(() => f.Services.Devices.CommandAsync("light-bedside", new DeviceCommand { On = true }))).Code);
            await Assert.ThrowsAsync<NotFoundException>(() => f.Services.Devices.CommandAsync("nope", new DeviceCommand { On = true }));
        }

        [Fact]
        public async Task Update_RenamesAndMovesBetweenRooms()
        {
            var f = new HomeFixture();
            var moved = await f.Services.Devices.UpdateAsync("light-bedside", "  Reading lamp ", "living-room");
            Assert.Equal("Reading lamp", moved.Name);
            Assert.Equal("living-room", moved.RoomId);

            var unassigned = await f.Services.Devices.UpdateAsync("light-bedside", "Reading lamp", null);
            Assert.Null(unassigned.RoomId);

            Assert.Equal(ErrorCodes.ValidationFailed, (await Assert.ThrowsAsync<DomainException>(() => f.Services.Devices.UpdateAsync("light-bedside", "x", "garage"))).Code);
            Assert.Equal(ErrorCodes.ValidationFailed, (await Assert.ThrowsAsync<DomainException>(() => f.Services.Devices.UpdateAsync("light-bedside", " ", null))).Code);
        }

        [Fact]
        public async Task Discovery_AddsNewLightsWithoutARoom_PreferringLanAndTheCloudName()
        {
            var f = new HomeFixture();
            var lan = new FakeDiscovery();
            lan.Result.Devices.Add(new DiscoveredDevice { HardwareId = "AA:01", Name = "Govee H6008", Type = DeviceType.Light, Driver = DriverKeys.GoveeLan, Address = "192.168.1.31" });
            var cloud = new FakeDiscovery();
            cloud.Result.Devices.Add(new DiscoveredDevice { HardwareId = "AA:01", Name = "Desk lamp", Type = DeviceType.Light, Driver = DriverKeys.GoveeCloud, Address = "H6008|AA:01" });
            cloud.Result.Devices.Add(new DiscoveredDevice { HardwareId = "BB:02", Name = "Hall strip", Type = DeviceType.Light, Driver = DriverKeys.GoveeCloud, Address = "H619A|BB:02" });
            cloud.Result.Notes.Add("cloud note");
            f.Discoveries.Add(lan);
            f.Discoveries.Add(cloud);

            var outcome = await f.Services.Devices.DiscoverAsync(default);

            Assert.Equal(2, outcome.Added.Count);
            var desk = outcome.Added.Single(d => d.HardwareId == "AA:01");
            Assert.Equal("Desk lamp", desk.Name);
            Assert.Equal(DriverKeys.GoveeLan, desk.Driver);
            Assert.Equal("192.168.1.31", desk.DriverAddress);
            Assert.Null(desk.RoomId);
            Assert.Contains("cloud note", outcome.Notes);
        }

        [Fact]
        public async Task Discovery_NeverDuplicates_AndFollowsANewAddress()
        {
            var f = new HomeFixture();
            var lan = new FakeDiscovery();
            lan.Result.Devices.Add(new DiscoveredDevice { HardwareId = "AA:01", Name = "Govee H6008", Type = DeviceType.Light, Driver = DriverKeys.GoveeLan, Address = "192.168.1.31" });
            f.Discoveries.Add(lan);
            var first = await f.Services.Devices.DiscoverAsync(default);
            await f.Services.Devices.UpdateAsync(first.Added.Single().Id, "Desk lamp", "bedroom");

            var again = await f.Services.Devices.DiscoverAsync(default);
            Assert.Empty(again.Added);
            Assert.Contains(again.Notes, n => n.StartsWith("No new lights"));

            lan.Result.Devices[0].Address = "192.168.1.77";
            var moved = await f.Services.Devices.DiscoverAsync(default);
            var light = moved.Updated.Single();
            Assert.Equal("192.168.1.77", light.DriverAddress);
            Assert.Equal("Desk lamp", f.Home.Devices.Single(d => d.HardwareId == "AA:01").Name);
            Assert.Equal(1, f.Home.Devices.Count(d => d.HardwareId == "AA:01"));
        }

        [Fact]
        public async Task Pairing_SavesTheAddressAndNeedsTheRightCode()
        {
            var f = new HomeFixture();
            f.Tv.Paired = false;
            Assert.True((await f.Services.Devices.GetAsync("tv")).NeedsPairing);

            var message = await f.Services.Devices.StartPairingAsync("tv", " 192.168.1.50 ");
            Assert.Equal("Enter the 6-character code shown on Dining room TV.", message);
            Assert.Equal("192.168.1.50", f.Home.Device("tv").DriverAddress);
            Assert.Equal("192.168.1.50", f.Tv.PairingStartedFor.Single());

            Assert.Equal(ErrorCodes.PairingFailed, (await Assert.ThrowsAsync<DomainException>(() => f.Services.Devices.ConfirmPairingAsync("tv", "000000"))).Code);

            var tv = await f.Services.Devices.ConfirmPairingAsync("tv", FakeTvDriver.CorrectCode);
            Assert.False(tv.NeedsPairing);
            Assert.True(tv.Online);
            Assert.False(tv.State.On);
        }

        [Fact]
        public async Task OnlyTheTvPairs()
        {
            var f = new HomeFixture();
            Assert.Equal(ErrorCodes.NotPairable, (await Assert.ThrowsAsync<DomainException>(() => f.Services.Devices.StartPairingAsync("light-bedside", null))).Code);
        }

        [Fact]
        public async Task Scenes_ApplyToEveryLightInTheirRooms()
        {
            var f = new HomeFixture();
            var activation = await f.Services.Scenes.ActivateAsync("bedtime");

            Assert.All(activation.Results, r => Assert.True(r.Ok));
            Assert.False(f.Home.Device("light-floor-lamp").State.On);
            Assert.Equal(25, f.Home.Device("light-bedside").State.Brightness);
        }
    }
}
