using Homebase.Domain.Automations;
using Homebase.Domain.Devices;
using Xunit;

namespace Homebase.Domain.Tests
{
    public class DescribeTests
    {
        private static readonly NameLookup Names = NameLookup.From(
            new[] { TestData.Phone(), TestData.Tv(), TestData.Light("light-floor-lamp", "Floor lamp") },
            new[] { new Scene { Id = "all-off", Name = "All off" } },
            new[] { new Room { Id = "bedroom", Name = "Bedroom" }, new Room { Id = "living-room", Name = "Living room" } });

        [Theory]
        [InlineData("phone", DeviceField.Home, false, null, "When Phone leaves home")]
        [InlineData("phone", DeviceField.Home, true, null, "When Phone arrives home")]
        [InlineData("phone", DeviceField.Charging, true, null, "When Phone starts charging")]
        [InlineData("phone", DeviceField.Asleep, false, null, "When Phone wakes up")]
        [InlineData("tv", DeviceField.On, false, null, "When Dining room TV turns off")]
        [InlineData("phone", DeviceField.Charging, true, 15, "When Phone has been charging for 15 min")]
        [InlineData("phone", DeviceField.Home, false, 10, "When Phone has been away for 10 min")]
        public void Triggers(string deviceId, DeviceField field, bool equals, int? forMinutes, string expected)
        {
            var trigger = new Trigger { Kind = TriggerKind.Device, DeviceId = deviceId, Field = field, EqualsValue = equals, ForMinutes = forMinutes };
            Assert.Equal(expected, Describe.Trigger(trigger, Names));
        }

        [Fact]
        public void Actions()
        {
            Assert.Equal("Living room lights → on at 70%, 2700 K", Describe.Action(HomeAction.ForRoom("living-room", new DeviceCommand { Brightness = 70, ColorTempK = 2700 }), Names));
            Assert.Equal("Bedroom lights → off", Describe.Action(HomeAction.ForRoom("bedroom", new DeviceCommand { On = false }), Names));
            Assert.Equal("Floor lamp → on", Describe.Action(HomeAction.ForDevice("light-floor-lamp", new DeviceCommand { On = true }), Names));
            Assert.Equal("Activate scene All off", Describe.Action(HomeAction.ForScene("all-off"), Names));
        }

        [Fact]
        public void Conditions()
        {
            Assert.Equal("Between 22:00 and 03:00", Describe.Condition(new Condition { Kind = ConditionKind.TimeWindow, After = "22:00", Before = "03:00" }, Names));
            Assert.Equal("It is dark", Describe.Condition(new Condition { Kind = ConditionKind.Dark, EqualsValue = true }, Names));
            Assert.Equal("It is light", Describe.Condition(new Condition { Kind = ConditionKind.Dark, EqualsValue = false }, Names));
            Assert.Equal("Phone is home", Describe.Condition(new Condition { Kind = ConditionKind.Device, DeviceId = "phone", Field = DeviceField.Home, EqualsValue = true }, Names));
        }
    }
}
