using System;

namespace Homebase.Domain
{
    /// <summary>Inject this instead of calling DateTime.UtcNow so time-dependent rules are testable.</summary>
    public interface IClock
    {
        DateTime UtcNow { get; }
    }

    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>
    /// UTC plus the household's time zone. Rules that talk about "06:45" or "today" use <see cref="LocalNow"/>;
    /// storage and the API always use UTC. Never use DateTime.Now (that is the server's zone, not the household's).
    /// </summary>
    public interface IHouseholdClock
    {
        DateTime UtcNow { get; }

        TimeZoneInfo TimeZone { get; }

        DateTime LocalNow { get; }

        DateTime ToLocal(DateTime utc);
    }

    public sealed class HouseholdClock : IHouseholdClock
    {
        private readonly IClock _clock;

        public HouseholdClock(IClock clock, TimeZoneInfo timeZone)
        {
            _clock = clock;
            TimeZone = timeZone ?? TimeZoneInfo.Utc;
        }

        public DateTime UtcNow => DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc);

        public TimeZoneInfo TimeZone { get; }

        public DateTime LocalNow => ToLocal(UtcNow);

        public DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone);
    }

    public static class HouseholdTimeZone
    {
        public const string EnvironmentVariable = "HOMEBASE_TIMEZONE";

        /// <summary>
        /// Resolves an IANA id such as "Europe/London" via TimeZoneInfo.FindSystemTimeZoneById. An empty id means the
        /// machine's own zone (Homebase runs on a Mac in the home); an unknown id falls back to UTC and sets usedFallback.
        /// </summary>
        public static TimeZoneInfo Resolve(string id, out bool usedFallback)
        {
            usedFallback = false;
            if (string.IsNullOrWhiteSpace(id))
            {
                return TimeZoneInfo.Local;
            }

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
            }
            catch (TimeZoneNotFoundException)
            {
                usedFallback = true;
            }
            catch (InvalidTimeZoneException)
            {
                usedFallback = true;
            }

            return TimeZoneInfo.Utc;
        }
    }
}
