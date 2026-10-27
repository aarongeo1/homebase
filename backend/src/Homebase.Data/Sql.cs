using System;
using System.Globalization;

namespace Homebase.Data
{
    /// <summary>SQLite has no date or boolean types; these keep the conversions in one place.</summary>
    internal static class Sql
    {
        public const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

        /// <summary>Fixed-width UTC text, so ORDER BY on the column is chronological.</summary>
        public static string Timestamp(DateTime utc) =>
            DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture);

        public static DateTime ParseTimestamp(string text) =>
            DateTime.ParseExact(text, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

        public static bool? Bool(long? value) => value.HasValue ? value.Value != 0 : (bool?)null;

        public static int? Int(long? value) => value.HasValue ? (int)value.Value : (int?)null;
    }
}
