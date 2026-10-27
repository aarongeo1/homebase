using System;
using System.Globalization;
using System.IO;
using Homebase.Domain.Sun;

namespace Homebase.Api.Host
{
    /// <summary>
    /// Everything configurable, from environment variables (see docs/ARCHITECTURE.md "Configuration"):
    /// HOMEBASE_URL, HOMEBASE_DATA, HOMEBASE_TIMEZONE, HOMEBASE_LATITUDE, HOMEBASE_LONGITUDE, HOMEBASE_GOVEE_API_KEY, HOMEBASE_WEB_ROOT.
    /// </summary>
    internal sealed class HostSettings
    {
        /// <summary>All interfaces, so the phone can reach it over Wi-Fi or Tailscale.</summary>
        public const string DefaultUrl = "http://0.0.0.0:5080";

        public string Url { get; private set; }

        public string DataDirectory { get; private set; }

        public string TimeZoneId { get; private set; }

        public GeoLocation Location { get; private set; }

        public string LocationError { get; private set; }

        public string GoveeApiKey { get; private set; }

        /// <summary>The built frontend (frontend/dist); null when it cannot be found (API only).</summary>
        public string WebRoot { get; private set; }

        public static HostSettings FromEnvironment()
        {
            var settings = new HostSettings
            {
                Url = Environment.GetEnvironmentVariable("HOMEBASE_URL") ?? DefaultUrl,
                DataDirectory = Environment.GetEnvironmentVariable("HOMEBASE_DATA") ?? DefaultDataDirectory(),
                TimeZoneId = Environment.GetEnvironmentVariable("HOMEBASE_TIMEZONE"),
                GoveeApiKey = Environment.GetEnvironmentVariable("HOMEBASE_GOVEE_API_KEY"),
                WebRoot = Environment.GetEnvironmentVariable("HOMEBASE_WEB_ROOT") ?? FindWebRoot(),
            };

            var latitude = Environment.GetEnvironmentVariable("HOMEBASE_LATITUDE");
            var longitude = Environment.GetEnvironmentVariable("HOMEBASE_LONGITUDE");
            if (!string.IsNullOrWhiteSpace(latitude) || !string.IsNullOrWhiteSpace(longitude))
            {
                double lat;
                double lon;
                if (double.TryParse(latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out lat)
                    && double.TryParse(longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out lon)
                    && lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180)
                {
                    settings.Location = new GeoLocation(lat, lon);
                }
                else
                {
                    settings.LocationError = "HOMEBASE_LATITUDE/HOMEBASE_LONGITUDE must both be decimal degrees (e.g. 51.5072 and -0.1276); ignoring them.";
                }
            }

            return settings;
        }

        private static string DefaultDataDirectory()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return OperatingSystem.IsMacOS()
                ? Path.Combine(home, "Library", "Application Support", "Homebase")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Homebase");
        }

        /// <summary>Walks up from the binary to the repo's frontend/dist, so `dotnet run` serves the built UI.</summary>
        private static string FindWebRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "frontend", "dist");
                if (File.Exists(Path.Combine(candidate, "index.html")))
                {
                    return candidate;
                }

                dir = dir.Parent;
            }

            return null;
        }
    }
}
