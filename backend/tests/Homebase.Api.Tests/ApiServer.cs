using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Homebase.Api.Infrastructure;
using Homebase.Application;
using Homebase.Devices;
using Homebase.Domain;
using Homebase.Domain.Devices;
using Homebase.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

namespace Homebase.Api.Tests
{
    /// <summary>The same AddHomebaseApi/UseHomebaseApi as the host, on an in-memory server.</summary>
    internal sealed class ApiServer : IAsyncDisposable
    {
        public const string Token = "test-report-token-0123456789";

        private readonly WebApplication _app;

        private ApiServer(WebApplication app, InMemoryHome home, FixedClock clock)
        {
            _app = app;
            Home = home;
            Clock = clock;
            Client = app.GetTestClient();
        }

        public InMemoryHome Home { get; }

        public FixedClock Clock { get; }

        public HttpClient Client { get; }

        public static async Task<ApiServer> StartAsync()
        {
            var clock = new FixedClock(TestHome.DefaultNow);
            var home = TestHome.Create(clock.UtcNow);
            var services = new HomeServices(
                home.AsRepositories(),
                new DriverRegistry(new IDeviceDriver[] { new SimulatedDriver(), new PushDriver(), new FakeTvDriver { Paired = false } }),
                new IDeviceDiscovery[0],
                new HouseholdClock(clock, TimeZoneInfo.Utc),
                null);

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddHomebaseApi(services, home, new ReportToken(Token));
            var app = builder.Build();
            app.UseHomebaseApi();
            await app.StartAsync();
            return new ApiServer(app, home, clock);
        }

        public static StringContent Json(string body) => new StringContent(body, Encoding.UTF8, "application/json");

        public async Task<JsonElement> ReadAsync(HttpResponseMessage response, HttpStatusCode expected)
        {
            var body = await response.Content.ReadAsStringAsync();
            if (response.StatusCode != expected)
            {
                throw new Xunit.Sdk.XunitException("Expected " + (int)expected + " but got " + (int)response.StatusCode + ": " + body);
            }

            return JsonDocument.Parse(body).RootElement;
        }

        /// <summary>A request that looks like it came from the Mac itself (loopback).</summary>
        public Task<HttpContextResponse> FromLoopbackAsync(string path) =>
            _app.GetTestServer().SendAsync(context =>
            {
                context.Request.Method = "GET";
                context.Request.Path = path;
                context.Connection.RemoteIpAddress = IPAddress.Loopback;
            }).ContinueWith(t => new HttpContextResponse(t.Result));

        public ValueTask DisposeAsync() => _app.DisposeAsync();
    }

    internal sealed class HttpContextResponse
    {
        public HttpContextResponse(Microsoft.AspNetCore.Http.HttpContext context)
        {
            Status = context.Response.StatusCode;
            using (var reader = new System.IO.StreamReader(context.Response.Body))
            {
                Body = JsonDocument.Parse(reader.ReadToEnd()).RootElement;
            }
        }

        public int Status { get; }

        public JsonElement Body { get; }
    }
}
