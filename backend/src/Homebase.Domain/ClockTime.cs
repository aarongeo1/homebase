using System;
using System.Globalization;

namespace Homebase.Domain
{
    /// <summary>A household-local "HH:mm" clock time (no date, no zone).</summary>
    public readonly struct ClockTime : IEquatable<ClockTime>, IComparable<ClockTime>
    {
        private ClockTime(int totalMinutes)
        {
            TotalMinutes = totalMinutes;
        }

        public int TotalMinutes { get; }

        public int Hour => TotalMinutes / 60;

        public int Minute => TotalMinutes % 60;

        public static ClockTime FromHourMinute(int hour, int minute)
        {
            if (hour < 0 || hour > 23 || minute < 0 || minute > 59)
            {
                throw new ArgumentOutOfRangeException(nameof(hour), "Clock time must be between 00:00 and 23:59.");
            }

            return new ClockTime((hour * 60) + minute);
        }

        public static ClockTime Of(DateTime local) => FromHourMinute(local.Hour, local.Minute);

        public static bool TryParse(string text, out ClockTime value)
        {
            value = default(ClockTime);
            DateTime parsed;
            if (text == null || text.Length != 5
                || !DateTime.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                return false;
            }

            value = FromHourMinute(parsed.Hour, parsed.Minute);
            return true;
        }

        public static ClockTime Parse(string text)
        {
            ClockTime value;
            if (!TryParse(text, out value))
            {
                throw new DomainException(ErrorCodes.ValidationFailed, "'" + text + "' is not a valid HH:mm time.");
            }

            return value;
        }

        public int CompareTo(ClockTime other) => TotalMinutes.CompareTo(other.TotalMinutes);

        public bool Equals(ClockTime other) => TotalMinutes == other.TotalMinutes;

        public override bool Equals(object obj) => obj is ClockTime other && Equals(other);

        public override int GetHashCode() => TotalMinutes;

        public override string ToString() => Hour.ToString("00", CultureInfo.InvariantCulture) + ":" + Minute.ToString("00", CultureInfo.InvariantCulture);
    }
}
