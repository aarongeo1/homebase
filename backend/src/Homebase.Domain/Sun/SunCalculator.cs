using System;
using Homebase.Domain.Automations;

namespace Homebase.Domain.Sun
{
    /// <summary>Where the home is, for sunrise and sunset (HOMEBASE_LATITUDE / HOMEBASE_LONGITUDE).</summary>
    public sealed class GeoLocation
    {
        public GeoLocation(double latitude, double longitude)
        {
            if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
            {
                throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be -90..90 and longitude -180..180.");
            }

            Latitude = latitude;
            Longitude = longitude;
        }

        public double Latitude { get; }

        /// <summary>East is positive.</summary>
        public double Longitude { get; }
    }

    /// <summary>Whether it is dark right now, and today's household-local sunrise and sunset.</summary>
    public sealed class SunInfo
    {
        public bool Dark { get; set; }

        /// <summary>Null without a location, or on a day the sun does not rise or set.</summary>
        public ClockTime? Sunrise { get; set; }

        public ClockTime? Sunset { get; set; }

        public bool LocationSet { get; set; }
    }

    /// <summary>
    /// NOAA solar position equations (accurate to about a minute away from the poles). Without a location,
    /// "dark" falls back to a fixed 18:00–07:00 window so rules still behave sensibly.
    /// </summary>
    public static class SunCalculator
    {
        /// <summary>Sun centre below this altitude = after sunset (refraction plus the solar disc's radius).</summary>
        public const double HorizonDegrees = -0.833;

        public static readonly ClockTime DefaultDarkFrom = ClockTime.FromHourMinute(18, 0);
        public static readonly ClockTime DefaultDarkUntil = ClockTime.FromHourMinute(7, 0);

        public static SunInfo Info(DateTime utc, GeoLocation location, TimeZoneInfo zone)
        {
            utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            zone = zone ?? TimeZoneInfo.Utc;
            var local = TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
            if (location == null)
            {
                return new SunInfo { Dark = AutomationEngine.InTimeWindow(ClockTime.Of(local), DefaultDarkFrom, DefaultDarkUntil), LocationSet = false };
            }

            DateTime? sunrise;
            DateTime? sunset;
            SunTimes(local.Date, location, out sunrise, out sunset);
            return new SunInfo
            {
                Dark = ElevationDegrees(utc, location) < HorizonDegrees,
                Sunrise = sunrise.HasValue ? ClockTime.Of(TimeZoneInfo.ConvertTimeFromUtc(sunrise.Value, zone)) : (ClockTime?)null,
                Sunset = sunset.HasValue ? ClockTime.Of(TimeZoneInfo.ConvertTimeFromUtc(sunset.Value, zone)) : (ClockTime?)null,
                LocationSet = true,
            };
        }

        /// <summary>The sun's altitude above the horizon in degrees at <paramref name="utc"/>.</summary>
        public static double ElevationDegrees(DateTime utc, GeoLocation location)
        {
            double declination;
            double equationOfTime;
            SolarTerms(JulianCentury(utc), out declination, out equationOfTime);

            var trueSolarMinutes = Mod(utc.TimeOfDay.TotalMinutes + equationOfTime + (4 * location.Longitude), 1440);
            var hourAngle = (trueSolarMinutes / 4) - 180;
            var lat = Rad(location.Latitude);
            var dec = Rad(declination);
            var cosZenith = (Math.Sin(lat) * Math.Sin(dec)) + (Math.Cos(lat) * Math.Cos(dec) * Math.Cos(Rad(hourAngle)));
            return 90 - Deg(Math.Acos(Math.Max(-1, Math.Min(1, cosZenith))));
        }

        /// <summary>Sunrise and sunset (UTC) on a calendar date at the location; null when the sun does not rise or set.</summary>
        public static void SunTimes(DateTime date, GeoLocation location, out DateTime? sunriseUtc, out DateTime? sunsetUtc)
        {
            var midnight = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);
            var noon = midnight.AddMinutes(720 - (4 * location.Longitude));
            double declination = 0;
            for (var i = 0; i < 2; i++)
            {
                double equationOfTime;
                SolarTerms(JulianCentury(noon), out declination, out equationOfTime);
                noon = midnight.AddMinutes(720 - (4 * location.Longitude) - equationOfTime);
            }

            var lat = Rad(location.Latitude);
            var dec = Rad(declination);
            var cosHourAngle = (Math.Cos(Rad(90 - HorizonDegrees)) / (Math.Cos(lat) * Math.Cos(dec))) - (Math.Tan(lat) * Math.Tan(dec));
            if (cosHourAngle > 1 || cosHourAngle < -1)
            {
                sunriseUtc = null;
                sunsetUtc = null;
                return;
            }

            var halfDayMinutes = 4 * Deg(Math.Acos(cosHourAngle));
            sunriseUtc = noon.AddMinutes(-halfDayMinutes);
            sunsetUtc = noon.AddMinutes(halfDayMinutes);
        }

        private static double JulianCentury(DateTime utc) => ((utc.ToOADate() + 2415018.5) - 2451545.0) / 36525.0;

        /// <summary>Solar declination (degrees) and the equation of time (minutes) for a Julian century.</summary>
        private static void SolarTerms(double t, out double declination, out double equationOfTime)
        {
            var meanLongitude = Mod(280.46646 + (t * (36000.76983 + (t * 0.0003032))), 360);
            var meanAnomaly = 357.52911 + (t * (35999.05029 - (0.0001537 * t)));
            var eccentricity = 0.016708634 - (t * (0.000042037 + (0.0000001267 * t)));
            var m = Rad(meanAnomaly);
            var centre = (Math.Sin(m) * (1.914602 - (t * (0.004817 + (0.000014 * t)))))
                + (Math.Sin(2 * m) * (0.019993 - (0.000101 * t)))
                + (Math.Sin(3 * m) * 0.000289);
            var omega = Rad(125.04 - (1934.136 * t));
            var apparentLongitude = Rad(meanLongitude + centre - 0.00569 - (0.00478 * Math.Sin(omega)));
            var meanObliquity = 23 + ((26 + ((21.448 - (t * (46.815 + (t * (0.00059 - (t * 0.001813)))))) / 60)) / 60);
            var obliquity = Rad(meanObliquity + (0.00256 * Math.Cos(omega)));

            declination = Deg(Math.Asin(Math.Sin(obliquity) * Math.Sin(apparentLongitude)));

            var y = Math.Pow(Math.Tan(obliquity / 2), 2);
            var l0 = Rad(meanLongitude);
            equationOfTime = 4 * Deg((y * Math.Sin(2 * l0))
                - (2 * eccentricity * Math.Sin(m))
                + (4 * eccentricity * y * Math.Sin(m) * Math.Cos(2 * l0))
                - (0.5 * y * y * Math.Sin(4 * l0))
                - (1.25 * eccentricity * eccentricity * Math.Sin(2 * m)));
        }

        private static double Rad(double degrees) => degrees * Math.PI / 180;

        private static double Deg(double radians) => radians * 180 / Math.PI;

        private static double Mod(double value, double modulus) => ((value % modulus) + modulus) % modulus;
    }
}
