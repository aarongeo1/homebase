using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using static Homebase.Api.Tests.ApiServer;

namespace Homebase.Api.Tests
{
    public class ApiTests
    {
        [Fact]
        public async Task Health()
        {
            await using (var api = await StartAsync())
            {
                var body = await api.ReadAsync(await api.Client.GetAsync("/api/health"), HttpStatusCode.OK);
                Assert.Equal("ok", body.GetProperty("status").GetString());
                Assert.Equal("ok", body.GetProperty("database").GetString());
            }
        }

        [Fact]
        public async Task Devices_AreCamelCaseWithNullsAndUtcTimestamps()
        {
            await using (var api = await StartAsync())
            {
                var devices = await api.ReadAsync(await api.Client.GetAsync("/api/devices"), HttpStatusCode.OK);
                var tv = devices.EnumerateArray().Single(d => d.GetProperty("id").GetString() == "tv");

                Assert.Equal("tv", tv.GetProperty("type").GetString());
                Assert.Equal("android-tv", tv.GetProperty("driver").GetString());
                Assert.True(tv.GetProperty("needsPairing").GetBoolean());
                Assert.Equal(System.Text.Json.JsonValueKind.Null, tv.GetProperty("state").GetProperty("brightness").ValueKind);
                Assert.Equal("2026-10-26T23:12:00Z", tv.GetProperty("lastChangedAt").GetString());
                Assert.Equal("phone", devices.EnumerateArray().Last().GetProperty("id").GetString()); // no room → last
            }
        }

        [Fact]
        public async Task Command_ChangesALight()
        {
            await using (var api = await StartAsync())
            {
                var lamp = await api.ReadAsync(await api.Client.PostAsync("/api/devices/light-bedside/commands", Json("{\"brightness\":30,\"colorTempK\":2200}")), HttpStatusCode.OK);
                Assert.True(lamp.GetProperty("state").GetProperty("on").GetBoolean());
                Assert.Equal(2200, lamp.GetProperty("state").GetProperty("colorTempK").GetInt32());
            }
        }

        [Theory]
        [InlineData("/api/devices/tv/commands", "{\"on\":true}", 422, "read_only_device")]
        [InlineData("/api/devices/nope/commands", "{\"on\":true}", 404, "not_found")]
        [InlineData("/api/devices/light-bedside/commands", "{\"brightness\":0}", 422, "validation_failed")]
        [InlineData("/api/devices/light-bedside/commands", "{bad json", 422, "validation_failed")]
        [InlineData("/api/devices/light-bedside/commands", "", 422, "validation_failed")]
        [InlineData("/api/devices/light-bedside/pairing", "{}", 422, "not_pairable")]
        [InlineData("/api/devices/tv/pairing/confirm", "{\"code\":\"\"}", 422, "validation_failed")]
        public async Task Errors_HaveStableCodes(string path, string body, int status, string code)
        {
            await using (var api = await StartAsync())
            {
                var error = await api.ReadAsync(await api.Client.PostAsync(path, Json(body)), (HttpStatusCode)status);
                Assert.Equal(code, error.GetProperty("error").GetProperty("code").GetString());
            }
        }

        [Fact]
        public async Task Report_NeedsTheToken_ThenRunsAutomations()
        {
            await using (var api = await StartAsync())
            {
                var denied = await api.ReadAsync(await api.Client.PostAsync("/api/devices/phone/report", Json("{\"home\":false}")), HttpStatusCode.Unauthorized);
                Assert.Equal("unauthorized", denied.GetProperty("error").GetProperty("code").GetString());

                var wrong = new HttpRequestMessage(HttpMethod.Post, "/api/devices/phone/report") { Content = Json("{\"home\":false}") };
                wrong.Headers.Add("Authorization", "Bearer nope");
                await api.ReadAsync(await api.Client.SendAsync(wrong), HttpStatusCode.Unauthorized);

                var request = new HttpRequestMessage(HttpMethod.Post, "/api/devices/phone/report") { Content = Json("{\"home\":false}") };
                request.Headers.Add("Authorization", "Bearer " + Token);
                var phone = await api.ReadAsync(await api.Client.SendAsync(request), HttpStatusCode.OK);

                Assert.False(phone.GetProperty("state").GetProperty("home").GetBoolean());
                var runs = await api.ReadAsync(await api.Client.GetAsync("/api/automations/runs?limit=5"), HttpStatusCode.OK);
                Assert.Equal("leave-all-off", runs[0].GetProperty("automationId").GetString());
                Assert.Equal("executed", runs[0].GetProperty("outcome").GetString());
            }
        }

        [Fact]
        public async Task Presence_ShowsTheTokenOnlyOnTheMac()
        {
            await using (var api = await StartAsync())
            {
                var remote = await api.ReadAsync(await api.Client.GetAsync("/api/presence"), HttpStatusCode.OK);
                Assert.Equal(System.Text.Json.JsonValueKind.Null, remote.GetProperty("reportToken").ValueKind);
                Assert.True(remote.GetProperty("home").GetBoolean());
                Assert.True(remote.GetProperty("sun").GetProperty("dark").GetBoolean());
                Assert.False(remote.GetProperty("sun").GetProperty("locationSet").GetBoolean());

                var local = await api.FromLoopbackAsync("/api/presence");
                Assert.Equal(200, local.Status);
                Assert.Equal(Token, local.Body.GetProperty("reportToken").GetString());
            }
        }

        [Fact]
        public async Task Update_RenamesAndUnassigns()
        {
            await using (var api = await StartAsync())
            {
                var lamp = await api.ReadAsync(await api.Client.PutAsync("/api/devices/light-bedside", Json("{\"name\":\"Reading lamp\",\"roomId\":null}")), HttpStatusCode.OK);
                Assert.Equal("Reading lamp", lamp.GetProperty("name").GetString());
                Assert.Equal(System.Text.Json.JsonValueKind.Null, lamp.GetProperty("roomId").ValueKind);
            }
        }

        [Fact]
        public async Task Automations_ShowTriggersConditionsAndWaiting()
        {
            await using (var api = await StartAsync())
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "/api/devices/phone/report") { Content = Json("{\"charging\":true}") };
                request.Headers.Add("Authorization", "Bearer " + Token);
                await api.ReadAsync(await api.Client.SendAsync(request), HttpStatusCode.OK);

                var automation = await api.ReadAsync(await api.Client.GetAsync("/api/automations/charging-at-night"), HttpStatusCode.OK);
                var trigger = automation.GetProperty("trigger");
                Assert.Equal("charging", trigger.GetProperty("field").GetString());
                Assert.True(trigger.GetProperty("equals").GetBoolean());
                Assert.Equal(15, trigger.GetProperty("forMinutes").GetInt32());
                Assert.Equal("When Phone has been charging for 15 min", trigger.GetProperty("summary").GetString());
                Assert.Equal("Between 21:00 and 05:00", automation.GetProperty("conditions")[0].GetProperty("summary").GetString());
                Assert.Equal("Bedroom lights → off", automation.GetProperty("actions")[0].GetProperty("summary").GetString());
                Assert.Equal("2026-10-26T23:27:00Z", automation.GetProperty("waitingUntil").GetString());
            }
        }

        [Fact]
        public async Task Simulate_AndEnable()
        {
            await using (var api = await StartAsync())
            {
                var simulation = await api.ReadAsync(await api.Client.PostAsync("/api/automations/arrive-after-dark/simulate", null), HttpStatusCode.OK);
                Assert.True(simulation.GetProperty("wouldRun").GetBoolean());
                Assert.Equal("Living room lights → on at 70%, 2700 K", simulation.GetProperty("plannedActions")[0].GetProperty("summary").GetString());

                var disabled = await api.ReadAsync(await api.Client.PutAsync("/api/automations/arrive-after-dark/enabled", Json("{\"enabled\":false}")), HttpStatusCode.OK);
                Assert.False(disabled.GetProperty("enabled").GetBoolean());
                await api.ReadAsync(await api.Client.PutAsync("/api/automations/arrive-after-dark/enabled", Json("{}")), (HttpStatusCode)422);
            }
        }

        [Fact]
        public async Task Scenes_AndUnknownRoutes()
        {
            await using (var api = await StartAsync())
            {
                var activation = await api.ReadAsync(await api.Client.PostAsync("/api/scenes/evening/activate", null), HttpStatusCode.OK);
                Assert.True(activation.GetProperty("results")[0].GetProperty("ok").GetBoolean());

                var missing = await api.ReadAsync(await api.Client.GetAsync("/api/energy/summary"), HttpStatusCode.NotFound);
                Assert.Equal("not_found", missing.GetProperty("error").GetProperty("code").GetString());
                await api.ReadAsync(await api.Client.GetAsync("/api/automations/runs?limit=abc"), (HttpStatusCode)422);
                await api.ReadAsync(await api.Client.GetAsync("/api/automations/runs?limit=500"), (HttpStatusCode)422);
            }
        }
    }
}
