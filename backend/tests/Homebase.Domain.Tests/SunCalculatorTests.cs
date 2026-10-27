using System;
using Homebase.Domain.Sun;
using Xunit;

namespace Homebase.Domain.Tests
{
    public class SunCalculatorTests
    {
        private static readonly GeoLocation London = new GeoLocation(51.5074, -0.1278);
        private static readonly GeoLocation Edmonton = new GeoLocation(53.5461, -113.4938);

        // Published times (timeanddate.com), UTC: London 2024-06-21 sunrise 03:43, sunset 20:21;
        // London 2024-12-21 sunrise 08:04, sunset 15:53; Edmonton 2024-06-21 sunrise 11:04, sunset 04:06 (next day UTC).
        [Theory]
        [InlineData(2024, 6, 21, 3, 43, 20, 21)]
        [InlineData(2024, 12, 21, 8, 4, 15, 53)]
        public void LondonSunriseAndSunset_WithinTwoMinutes(int year, int month, int day, int riseH, int riseM, int setH, int setM)
        {
            DateTime? rise;
            DateTime? set;
            SunCalculator.SunTimes(new DateTime(year, month, day), London, out rise, out set);

            AssertNear(new DateTime(year, month, day, riseH, riseM, 0, DateTimeKind.Utc), rise.Value);
            AssertNear(new DateTime(year, month, day, setH, setM, 0, DateTimeKind.Utc), set.Value);
        }

        [Fact]
        public void EdmontonSummer_LocalTimesFromTheHouseholdZone()
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Edmonton");
            var info = SunCalculator.Info(new DateTime(2024, 6, 21, 18, 0, 0, DateTimeKind.Utc), Edmonton, zone);

            Assert.True(info.LocationSet);
            Assert.False(info.Dark);
            Assert.InRange(info.Sunrise.Value.TotalMinutes, (5 * 60) + 2, (5 * 60) + 6); // 05:04 MDT
            Assert.InRange(info.Sunset.Value.TotalMinutes, (22 * 60) + 4, (22 * 60) + 8); // 22:06 MDT
        }

        [Fact]
        public void DarkAfterSunsetAndLightAtNoon()
        {
            Assert.True(SunCalculator.Info(new DateTime(2024, 12, 21, 17, 0, 0, DateTimeKind.Utc), London, TimeZoneInfo.Utc).Dark);
            Assert.False(SunCalculator.Info(new DateTime(2024, 12, 21, 12, 0, 0, DateTimeKind.Utc), London, TimeZoneInfo.Utc).Dark);
        }

        [Fact]
        public void PolarNight_HasNoSunrise()
        {
            DateTime? rise;
            DateTime? set;
            SunCalculator.SunTimes(new DateTime(2024, 12, 21), new GeoLocation(78.22, 15.65), out rise, out set);
            Assert.Null(rise);
            Assert.Null(set);
        }

        [Theory]
        [InlineData(17, 59, false)]
        [InlineData(18, 0, true)]
        [InlineData(6, 59, true)]
        [InlineData(7, 0, false)]
        public void WithoutALocation_DarkMeans18To7(int hour, int minute, bool dark)
        {
            var info = SunCalculator.Info(new DateTime(2026, 10, 26, hour, minute, 0, DateTimeKind.Utc), null, TimeZoneInfo.Utc);
            Assert.False(info.LocationSet);
            Assert.Equal(dark, info.Dark);
            Assert.Null(info.Sunrise);
        }

        private static void AssertNear(DateTime expected, DateTime actual) =>
            Assert.InRange((actual - expected).TotalMinutes, -2, 2);
    }
}
