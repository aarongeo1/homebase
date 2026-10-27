using System;
using System.IO;
using System.Net.Http;
using Homebase.Api.Infrastructure;
using Homebase.Application;
using Homebase.Data;
using Homebase.Data.Repositories;
using Homebase.Devices;
using Homebase.Devices.AndroidTv;
using Homebase.Devices.Govee;
using Homebase.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace Homebase.Api.Host
{
    /// <summary>
    /// Homebase on the Mac: the API, the scheduler and the built frontend on one port (default 5080).
    /// Settings come from environment variables (<see cref="HostSettings"/>); data lives in one SQLite file.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var settings = HostSettings.FromEnvironment();
            Directory.CreateDirectory(settings.DataDirectory);

            var connections = new SqliteConnectionFactory(Path.Combine(settings.DataDirectory, "homebase.db"));
            var applied = MigrationRunner.Run(connections);

            bool usedFallback;
            var zone = HouseholdTimeZone.Resolve(settings.TimeZoneId, out usedFallback);

            var repositories = new HomeRepositories
            {
                Rooms = new SqliteRoomRepository(connections),
                Devices = new SqliteDeviceRepository(connections),
                Scenes = new SqliteSceneRepository(connections),
                Automations = new SqliteAutomationRepository(connections),
                Settings = new SqliteSettingsRepository(connections),
            };

            var http = new HttpClient();
            var goveeLan = new GoveeLanClient(new UdpTransport(GoveeLanClient.ResponsePort));
            var goveeCloud = new GoveeCloudClient(http, settings.GoveeApiKey);
            var androidTv = new AndroidTvDriver(repositories.Settings);
            var drivers = new DriverRegistry(new IDeviceDriver[]
            {
                new SimulatedDriver(), new PushDriver(), new GoveeLanDriver(goveeLan), new GoveeCloudDriver(goveeCloud), androidTv,
            });
            var discoveries = new IDeviceDiscovery[] { new GoveeLanDiscovery(goveeLan), new GoveeCloudDiscovery(goveeCloud) };
            var home = new HomeServices(repositories, drivers, discoveries, new HouseholdClock(new SystemClock(), zone), settings.Location);

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });
            builder.WebHost.UseUrls(settings.Url);
            builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
            builder.Services.AddHomebaseApi(home, new SqliteDatabaseHealthCheck(connections), ReportToken.LoadOrCreate(settings.DataDirectory));
            builder.Services.AddHostedService<SchedulerService>();

            var app = builder.Build();
            var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Homebase");
            if (applied.Count > 0) log.LogInformation("Applied migrations: {Scripts}", string.Join(", ", applied));
            if (usedFallback) log.LogWarning("Unknown time zone '{Zone}'; using UTC. Use an IANA id such as 'Europe/London'.", settings.TimeZoneId);
            if (settings.LocationError != null) log.LogWarning(settings.LocationError);
            if (settings.Location == null) log.LogInformation("No location set: 'dark' means 18:00–07:00. Set HOMEBASE_LATITUDE and HOMEBASE_LONGITUDE for real sunset times.");

            if (settings.WebRoot != null)
            {
                var files = new PhysicalFileProvider(settings.WebRoot);
                app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
                app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
            }
            else
            {
                log.LogWarning("frontend/dist not found: serving the API only. Run `npm run build` (or set HOMEBASE_WEB_ROOT).");
            }

            app.UseHomebaseApi();
            if (settings.WebRoot != null)
            {
                // Client-side routes (/devices, /presence, ...) load the app; /api never falls through to it.
                app.MapFallbackToFile("{*path:regex(^(?!api/).*$)}", "index.html", new StaticFileOptions { FileProvider = new PhysicalFileProvider(settings.WebRoot) });
            }

            log.LogInformation("Homebase on {Url} (household time zone {Zone}; data in {Data})", settings.Url, zone.Id, settings.DataDirectory);
            app.Run();

            androidTv.Dispose();
            goveeLan.Dispose();
            return 0;
        }
    }
}
