using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Homebase.Devices.Govee;
using Homebase.Domain;
using Homebase.Domain.Devices;
using Homebase.Testing;
using Xunit;

namespace Homebase.Devices.Tests
{
    public class GoveeCloudTests
    {
        private static Device Strip() => new Device
        {
            Id = "light-tv-strip",
            Name = "TV backlight",
            Type = DeviceType.Light,
            Driver = DriverKeys.GoveeCloud,
            DriverAddress = GoveeCloudDriver.Address("H605C", "64:09:C5:32:37:36:2D:13"),
            Online = true,
            State = new DeviceState { On = false, Brightness = 50 },
        };

        [Fact]
        public async Task Discovery_WithoutAKey_ExplainsWhereToGetOne()
        {
            var result = await new GoveeCloudDiscovery(new GoveeCloudClient(new HttpClient(new FakeHttpHandler(_ => null)), null)).DiscoverAsync(default);
            Assert.Empty(result.Devices);
            Assert.Contains("HOMEBASE_GOVEE_API_KEY", result.Notes.Single());
        }

        [Fact]
        public async Task Discovery_ListsLightsOnly_WithTheKeyHeader()
        {
            string key = null;
            var handler = new FakeHttpHandler(request =>
            {
                key = request.Headers.GetValues(GoveeCloudClient.ApiKeyHeader).Single();
                return FakeHttpHandler.Json("{\"code\":200,\"message\":\"success\",\"data\":["
                    + "{\"sku\":\"H605C\",\"device\":\"64:09\",\"deviceName\":\"TV backlight\",\"type\":\"devices.types.light\"},"
                    + "{\"sku\":\"H7121\",\"device\":\"11:22\",\"deviceName\":\"Air purifier\",\"type\":\"devices.types.air_purifier\"}]}");
            });

            var result = await new GoveeCloudDiscovery(new GoveeCloudClient(new HttpClient(handler), "secret-key")).DiscoverAsync(default);

            Assert.Equal("secret-key", key);
            Assert.Equal("https://openapi.api.govee.com/router/api/v1/user/devices", handler.Requests.Single());
            var light = result.Devices.Single();
            Assert.Equal("TV backlight", light.Name);
            Assert.Equal("H605C|64:09", light.Address);
            Assert.Equal("64:09", light.HardwareId);
        }

        [Fact]
        public async Task Apply_SendsOneControlCallPerCapability()
        {
            var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json("{\"requestId\":\"x\",\"msg\":\"success\",\"code\":200}"));
            var driver = new GoveeCloudDriver(new GoveeCloudClient(new HttpClient(handler), "k"));

            var state = await driver.ApplyAsync(Strip(), new DeviceCommand { Brightness = 40, ColorTempK = 2700 });

            Assert.Equal(3, handler.Requests.Count);
            Assert.All(handler.Requests, r => Assert.EndsWith("/device/control", r));
            Assert.True(state.On);
            Assert.Equal(40, state.Brightness);
        }

        [Fact]
        public async Task Read_MapsCapabilities_AndOfflineMeansOffline()
        {
            var body = "{\"code\":200,\"msg\":\"success\",\"payload\":{\"capabilities\":["
                + "{\"type\":\"devices.capabilities.online\",\"instance\":\"online\",\"state\":{\"value\":true}},"
                + "{\"type\":\"devices.capabilities.on_off\",\"instance\":\"powerSwitch\",\"state\":{\"value\":1}},"
                + "{\"type\":\"devices.capabilities.range\",\"instance\":\"brightness\",\"state\":{\"value\":65}},"
                + "{\"type\":\"devices.capabilities.color_setting\",\"instance\":\"colorTemperatureK\",\"state\":{\"value\":3500}}]}}";
            var state = await new GoveeCloudDriver(new GoveeCloudClient(new HttpClient(new FakeHttpHandler(_ => FakeHttpHandler.Json(body))), "k")).ReadAsync(Strip());
            Assert.True(state.On);
            Assert.Equal(65, state.Brightness);
            Assert.Equal(3500, state.ColorTempK);

            var offline = body.Replace("{\"value\":true}", "{\"value\":false}");
            await Assert.ThrowsAsync<DeviceOfflineException>(() => new GoveeCloudDriver(new GoveeCloudClient(new HttpClient(new FakeHttpHandler(_ => FakeHttpHandler.Json(offline))), "k")).ReadAsync(Strip()));
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized, "rejected the API key")]
        [InlineData((HttpStatusCode)429, "rate limit")]
        [InlineData(HttpStatusCode.BadGateway, "HTTP 502")]
        public async Task HttpErrors_BecomeOfflineWithAReason(HttpStatusCode status, string reason)
        {
            var driver = new GoveeCloudDriver(new GoveeCloudClient(new HttpClient(new FakeHttpHandler(_ => new HttpResponseMessage(status))), "k"));
            var ex = await Assert.ThrowsAsync<DeviceOfflineException>(() => driver.ReadAsync(Strip()));
            Assert.Contains(reason, ex.Message);
        }
    }
}
