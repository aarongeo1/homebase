using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Homebase.Devices.Govee;
using Homebase.Domain;
using Homebase.Domain.Devices;
using Xunit;

namespace Homebase.Devices.Tests
{
    /// <summary>A pretend network: records what was sent and lets the test decide what the lights answer.</summary>
    internal sealed class FakeUdpTransport : IUdpTransport
    {
        private Action<IPEndPoint, byte[]> _onReceive;

        public List<Tuple<IPEndPoint, string>> Sent { get; } = new List<Tuple<IPEndPoint, string>>();

        public Func<IPEndPoint, string, IEnumerable<Tuple<IPEndPoint, string>>> Answer { get; set; } = (to, message) => Enumerable.Empty<Tuple<IPEndPoint, string>>();

        public bool PortTaken { get; set; }

        public void Start(Action<IPEndPoint, byte[]> onReceive)
        {
            if (PortTaken) throw new SocketException((int)SocketError.AddressAlreadyInUse);
            _onReceive = onReceive;
        }

        public Task SendAsync(byte[] datagram, IPEndPoint to)
        {
            var text = Encoding.UTF8.GetString(datagram);
            Sent.Add(Tuple.Create(to, text));
            foreach (var reply in Answer(to, text).ToList())
            {
                _onReceive(reply.Item1, Encoding.UTF8.GetBytes(reply.Item2));
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }

    public class GoveeLanTests
    {
        private const string LampIp = "192.168.1.31";
        private const string StatusReply = "{\"msg\":{\"cmd\":\"devStatus\",\"data\":{\"onOff\":1,\"brightness\":40,\"color\":{\"r\":255,\"g\":255,\"b\":255},\"colorTemInKelvin\":2700}}}";

        private static Device Lamp() => new Device { Id = "light-bedside", Name = "Bedside lamp", Type = DeviceType.Light, Driver = DriverKeys.GoveeLan, DriverAddress = LampIp, Online = true, State = new DeviceState { On = false } };

        private static FakeUdpTransport AnsweringLamp() => new FakeUdpTransport
        {
            Answer = (to, message) => message.Contains("\"devStatus\"") && to.Address.ToString() == LampIp
                ? new[] { Tuple.Create(new IPEndPoint(IPAddress.Parse(LampIp), GoveeLanClient.CommandPort), StatusReply) }
                : Enumerable.Empty<Tuple<IPEndPoint, string>>(),
        };

        [Fact]
        public void Messages_MatchTheWlanGuide()
        {
            Assert.Equal("{\"msg\":{\"cmd\":\"scan\",\"data\":{\"account_topic\":\"reserve\"}}}", Encoding.UTF8.GetString(GoveeLanMessages.Scan()));
            Assert.Equal("{\"msg\":{\"cmd\":\"turn\",\"data\":{\"value\":1}}}", Encoding.UTF8.GetString(GoveeLanMessages.Turn(true)));
            Assert.Equal("{\"msg\":{\"cmd\":\"brightness\",\"data\":{\"value\":25}}}", Encoding.UTF8.GetString(GoveeLanMessages.Brightness(25)));
            Assert.Equal("{\"msg\":{\"cmd\":\"colorwc\",\"data\":{\"color\":{\"r\":255,\"g\":255,\"b\":255},\"colorTemInKelvin\":2200}}}", Encoding.UTF8.GetString(GoveeLanMessages.ColorTemperature(2200)));
            Assert.Equal("{\"msg\":{\"cmd\":\"devStatus\",\"data\":{}}}", Encoding.UTF8.GetString(GoveeLanMessages.DevStatus()));
        }

        [Fact]
        public async Task Scan_CollectsEveryLightThatAnswers()
        {
            var transport = new FakeUdpTransport
            {
                Answer = (to, message) => to.Equals(GoveeLanClient.ScanEndpoint)
                    ? new[]
                    {
                        Tuple.Create(new IPEndPoint(IPAddress.Parse("192.168.1.31"), 4001), "{\"msg\":{\"cmd\":\"scan\",\"data\":{\"ip\":\"192.168.1.31\",\"device\":\"1F:80:C5:32:32:36:72:4E\",\"sku\":\"H6008\"}}}"),
                        Tuple.Create(new IPEndPoint(IPAddress.Parse("192.168.1.32"), 4001), "{\"msg\":{\"cmd\":\"scan\",\"data\":{\"device\":\"AA:BB\",\"sku\":\"H619A\"}}}"),
                        Tuple.Create(new IPEndPoint(IPAddress.Parse("192.168.1.33"), 4001), "not json"),
                    }
                    : Enumerable.Empty<Tuple<IPEndPoint, string>>(),
            };

            var result = await new GoveeLanDiscovery(new GoveeLanClient(transport), TimeSpan.Zero).DiscoverAsync(default);

            Assert.Equal(new[] { "192.168.1.31", "192.168.1.32" }, result.Devices.Select(d => d.Address));
            Assert.Equal("Govee H6008", result.Devices[0].Name);
            Assert.All(result.Devices, d => Assert.Equal(DriverKeys.GoveeLan, d.Driver));
            Assert.Empty(result.Notes);
        }

        [Fact]
        public async Task Scan_ExplainsLanControlWhenNothingAnswers()
        {
            var result = await new GoveeLanDiscovery(new GoveeLanClient(new FakeUdpTransport()), TimeSpan.Zero).DiscoverAsync(default);
            Assert.Empty(result.Devices);
            Assert.Contains("LAN Control", result.Notes.Single());
        }

        [Fact]
        public async Task Scan_ExplainsABusyPort()
        {
            var result = await new GoveeLanDiscovery(new GoveeLanClient(new FakeUdpTransport { PortTaken = true }), TimeSpan.Zero).DiscoverAsync(default);
            Assert.Contains("UDP port 4002", result.Notes.Single());
        }

        [Fact]
        public async Task Apply_SendsTurnBrightnessAndColourToPort4003_ThenReadsBack()
        {
            var transport = AnsweringLamp();
            var driver = new GoveeLanDriver(new GoveeLanClient(transport), TimeSpan.Zero);

            var state = await driver.ApplyAsync(Lamp(), new DeviceCommand { Brightness = 25, ColorTempK = 2200 });

            var commands = transport.Sent.Select(s => JsonDocument.Parse(s.Item2).RootElement.GetProperty("msg").GetProperty("cmd").GetString()).ToList();
            Assert.Equal(new[] { "turn", "brightness", "colorwc", "devStatus" }, commands);
            Assert.All(transport.Sent, s => Assert.Equal(GoveeLanClient.CommandPort, s.Item1.Port));
            Assert.True(state.On);
            Assert.Equal(25, state.Brightness); // what was asked for wins over a stale status reply
            Assert.Equal(2200, state.ColorTempK);
        }

        [Fact]
        public async Task Read_MapsTheStatusReply()
        {
            var state = await new GoveeLanDriver(new GoveeLanClient(AnsweringLamp())).ReadAsync(Lamp());
            Assert.True(state.On);
            Assert.Equal(40, state.Brightness);
            Assert.Equal(2700, state.ColorTempK);
        }

        [Fact]
        public async Task SilentLight_IsOffline()
        {
            var ex = await Assert.ThrowsAsync<DeviceOfflineException>(() => new GoveeLanDriver(new GoveeLanClient(new FakeUdpTransport())).ReadAsync(Lamp()));
            Assert.Contains("no response within 2 s", ex.Message);
        }

        [Fact]
        public void ColourMode_ReportsNoColourTemperature()
        {
            string cmd;
            JsonElement data;
            Assert.True(GoveeLanMessages.TryParseEnvelope(Encoding.UTF8.GetBytes("{\"msg\":{\"cmd\":\"devStatus\",\"data\":{\"onOff\":0,\"brightness\":100,\"colorTemInKelvin\":0}}}"), out cmd, out data));
            var status = GoveeLanMessages.ParseStatus(data);
            Assert.False(status.On);
            Assert.Null(status.ColorTempK);
        }
    }
}
