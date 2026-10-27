using Homebase.Domain.Devices;
using Xunit;
using static Homebase.Domain.Tests.TestData;

namespace Homebase.Domain.Tests
{
    public class DeviceCommandsTests
    {
        [Fact]
        public void BrightnessTurnsALightOn()
        {
            var state = DeviceCommands.Apply(Light(on: false), new DeviceCommand { Brightness = 25, ColorTempK = 2200 });
            Assert.True(state.On);
            Assert.Equal(25, state.Brightness);
            Assert.Equal(2200, state.ColorTempK);
        }

        [Theory]
        [InlineData(0, null, null)]
        [InlineData(101, null, null)]
        [InlineData(null, 1999, null)]
        [InlineData(null, 9001, null)]
        [InlineData(50, null, false)]
        public void InvalidLightCommands(int? brightness, int? kelvin, bool? on)
        {
            var ex = Assert.Throws<DomainException>(() => DeviceCommands.Validate(Light(), new DeviceCommand { Brightness = brightness, ColorTempK = kelvin, On = on }));
            Assert.Equal(ErrorCodes.ValidationFailed, ex.Code);
        }

        [Fact]
        public void EmptyCommand_IsRejected()
        {
            Assert.Equal(ErrorCodes.ValidationFailed, Assert.Throws<DomainException>(() => DeviceCommands.Validate(Light(), new DeviceCommand())).Code);
        }

        [Fact]
        public void TvAndPhone_AreReadOnly()
        {
            Assert.Equal(ErrorCodes.ReadOnlyDevice, Assert.Throws<DomainException>(() => DeviceCommands.Validate(Tv(), new DeviceCommand { On = true })).Code);
            Assert.Equal(ErrorCodes.ReadOnlyDevice, Assert.Throws<DomainException>(() => DeviceCommands.Validate(Phone(), new DeviceCommand { On = true })).Code);
        }

        [Fact]
        public void OfflineLight_IsRejected()
        {
            Assert.Equal(ErrorCodes.DeviceOffline, Assert.Throws<DeviceOfflineException>(() => DeviceCommands.Validate(Light(online: false), new DeviceCommand { On = true })).Code);
        }

        [Fact]
        public void PhoneReports_ChangeOnlyTheGivenFields()
        {
            var phone = Phone(home: true, charging: false, asleep: false);
            var report = new DeviceReport { Charging = true };
            DeviceReports.Validate(phone, report);
            var state = DeviceReports.Apply(phone, report);
            Assert.True(state.Home);
            Assert.True(state.Charging);
            Assert.False(state.Asleep);
        }

        [Fact]
        public void Reports_OnlyFromPushDevices_AndNeverEmpty()
        {
            Assert.Equal(ErrorCodes.NotReportable, Assert.Throws<DomainException>(() => DeviceReports.Validate(Tv(), new DeviceReport { Home = true })).Code);
            Assert.Equal(ErrorCodes.ValidationFailed, Assert.Throws<DomainException>(() => DeviceReports.Validate(Phone(), new DeviceReport())).Code);
        }

        [Fact]
        public void Normalize_ClearsFieldsTheTypeDoesNotHave()
        {
            var state = DeviceCapabilities.Normalize(DeviceType.Tv, new DeviceState { On = true, Brightness = 40, Home = true });
            Assert.True(state.On);
            Assert.Null(state.Brightness);
            Assert.Null(state.Home);
        }
    }
}
