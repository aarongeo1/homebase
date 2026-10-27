using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Homebase.Domain;
using Homebase.Domain.Devices;

namespace Homebase.Devices.Govee
{
    /// <summary>UDP send/receive, behind an interface so the Govee client can be tested without a network.</summary>
    public interface IUdpTransport : IDisposable
    {
        /// <summary>Starts listening; <paramref name="onReceive"/> runs for every datagram. Throws SocketException if the port is taken.</summary>
        void Start(Action<IPEndPoint, byte[]> onReceive);

        Task SendAsync(byte[] datagram, IPEndPoint to);
    }

    public sealed class UdpTransport : IUdpTransport
    {
        private readonly int _listenPort;
        private UdpClient _client;

        public UdpTransport(int listenPort)
        {
            _listenPort = listenPort;
        }

        public void Start(Action<IPEndPoint, byte[]> onReceive)
        {
            var client = new UdpClient(AddressFamily.InterNetwork);
            try
            {
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                client.Client.Bind(new IPEndPoint(IPAddress.Any, _listenPort));
            }
            catch
            {
                client.Dispose();
                throw;
            }

            _client = client;
            _ = ReceiveLoopAsync(client, onReceive);
        }

        public Task SendAsync(byte[] datagram, IPEndPoint to) => _client.SendAsync(datagram, datagram.Length, to);

        public void Dispose() => _client?.Dispose();

        private static async Task ReceiveLoopAsync(UdpClient client, Action<IPEndPoint, byte[]> onReceive)
        {
            while (true)
            {
                UdpReceiveResult received;
                try
                {
                    received = await client.ReceiveAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    // e.g. ICMP port unreachable from an earlier send; keep listening.
                    continue;
                }

                onReceive(received.RemoteEndPoint, received.Buffer);
            }
        }
    }

    public sealed class GoveeLanDevice
    {
        public string Ip { get; set; }

        /// <summary>Govee's device id, e.g. "1F:80:C5:32:32:36:72:4E".</summary>
        public string DeviceId { get; set; }

        /// <summary>Model, e.g. "H6008".</summary>
        public string Sku { get; set; }
    }

    public sealed class GoveeLanStatus
    {
        public bool On { get; set; }

        public int? Brightness { get; set; }

        public int? ColorTempK { get; set; }
    }

    /// <summary>Thrown when Homebase cannot listen for Govee replies (another app holds UDP port 4002).</summary>
    public sealed class GoveeLanUnavailableException : Exception
    {
        public GoveeLanUnavailableException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    /// <summary>
    /// Govee's local "LAN API" (WLAN guide): JSON over UDP. Scan requests go to multicast 239.255.255.250:4001, commands
    /// to the light on port 4003, and every reply comes back to port 4002. Only lights with "LAN Control" switched on in
    /// the Govee Home app answer. One instance per process, because only one socket can own port 4002.
    /// </summary>
    public sealed class GoveeLanClient : IDisposable
    {
        public const int ScanPort = 4001;
        public const int ResponsePort = 4002;
        public const int CommandPort = 4003;
        public static readonly IPEndPoint ScanEndpoint = new IPEndPoint(IPAddress.Parse("239.255.255.250"), ScanPort);

        private readonly IUdpTransport _transport;
        private readonly object _gate = new object();
        private readonly ConcurrentDictionary<int, Action<IPEndPoint, string, JsonElement>> _listeners = new ConcurrentDictionary<int, Action<IPEndPoint, string, JsonElement>>();
        private int _nextListener;
        private bool _started;

        public GoveeLanClient(IUdpTransport transport)
        {
            _transport = transport;
        }

        public async Task<IReadOnlyList<GoveeLanDevice>> ScanAsync(TimeSpan listenFor)
        {
            EnsureStarted();
            var found = new ConcurrentDictionary<string, GoveeLanDevice>(StringComparer.Ordinal);
            using (Listen((from, cmd, data) =>
            {
                var device = cmd == "scan" ? GoveeLanMessages.ParseScan(data, from) : null;
                if (device != null) found[device.DeviceId] = device;
            }))
            {
                await _transport.SendAsync(GoveeLanMessages.Scan(), ScanEndpoint).ConfigureAwait(false);
                await Task.Delay(listenFor).ConfigureAwait(false);
            }

            return found.Values.OrderBy(d => d.Ip, StringComparer.Ordinal).ToList();
        }

        public Task SendAsync(string ip, byte[] message)
        {
            EnsureStarted();
            return _transport.SendAsync(message, new IPEndPoint(ParseIp(ip), CommandPort));
        }

        /// <summary>Asks the light for its state; null when it does not answer within <paramref name="timeout"/>.</summary>
        public async Task<GoveeLanStatus> StatusAsync(string ip, TimeSpan timeout)
        {
            EnsureStarted();
            var address = ParseIp(ip);
            var reply = new TaskCompletionSource<GoveeLanStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (Listen((from, cmd, data) =>
            {
                if (cmd == "devStatus" && from.Address.Equals(address)) reply.TrySetResult(GoveeLanMessages.ParseStatus(data));
            }))
            {
                await _transport.SendAsync(GoveeLanMessages.DevStatus(), new IPEndPoint(address, CommandPort)).ConfigureAwait(false);
                var first = await Task.WhenAny(reply.Task, Task.Delay(timeout)).ConfigureAwait(false);
                return first == reply.Task ? reply.Task.Result : null;
            }
        }

        public void Dispose() => _transport.Dispose();

        private static IPAddress ParseIp(string ip)
        {
            IPAddress address;
            if (string.IsNullOrWhiteSpace(ip) || !IPAddress.TryParse(ip.Trim(), out address))
            {
                throw new GoveeLanUnavailableException("'" + ip + "' is not an IP address.", null);
            }

            return address;
        }

        private void EnsureStarted()
        {
            lock (_gate)
            {
                if (_started)
                {
                    return;
                }

                try
                {
                    _transport.Start(OnReceive);
                }
                catch (SocketException ex)
                {
                    throw new GoveeLanUnavailableException("cannot listen on UDP port " + ResponsePort + " (is another Govee app running?): " + ex.Message, ex);
                }

                _started = true;
            }
        }

        private IDisposable Listen(Action<IPEndPoint, string, JsonElement> listener)
        {
            var id = Interlocked.Increment(ref _nextListener);
            _listeners[id] = listener;
            return new Unsubscribe(() =>
            {
                Action<IPEndPoint, string, JsonElement> removed;
                _listeners.TryRemove(id, out removed);
            });
        }

        private void OnReceive(IPEndPoint from, byte[] datagram)
        {
            string cmd;
            JsonElement data;
            if (!GoveeLanMessages.TryParseEnvelope(datagram, out cmd, out data))
            {
                return;
            }

            foreach (var listener in _listeners.Values)
            {
                listener(from, cmd, data);
            }
        }

        private sealed class Unsubscribe : IDisposable
        {
            private readonly Action _action;

            public Unsubscribe(Action action)
            {
                _action = action;
            }

            public void Dispose() => _action();
        }
    }

    /// <summary>Builds and parses Govee LAN API messages: {"msg":{"cmd":"...","data":{...}}}.</summary>
    public static class GoveeLanMessages
    {
        public static byte[] Scan() => Message("scan", new { account_topic = "reserve" });

        public static byte[] Turn(bool on) => Message("turn", new { value = on ? 1 : 0 });

        public static byte[] Brightness(int percent) => Message("brightness", new { value = percent });

        /// <summary>White at a colour temperature; Govee ignores the RGB value when colorTemInKelvin is set.</summary>
        public static byte[] ColorTemperature(int kelvin) => Message("colorwc", new { color = new { r = 255, g = 255, b = 255 }, colorTemInKelvin = kelvin });

        public static byte[] DevStatus() => Message("devStatus", new { });

        public static bool TryParseEnvelope(byte[] datagram, out string cmd, out JsonElement data)
        {
            cmd = null;
            data = default(JsonElement);
            try
            {
                using (var doc = JsonDocument.Parse(datagram))
                {
                    JsonElement msg;
                    JsonElement cmdElement;
                    if (!doc.RootElement.TryGetProperty("msg", out msg) || !msg.TryGetProperty("cmd", out cmdElement) || cmdElement.ValueKind != JsonValueKind.String)
                    {
                        return false;
                    }

                    cmd = cmdElement.GetString();
                    JsonElement payload;
                    data = msg.TryGetProperty("data", out payload) ? payload.Clone() : default(JsonElement);
                    return true;
                }
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>Null when the reply has no device id.</summary>
        public static GoveeLanDevice ParseScan(JsonElement data, IPEndPoint from)
        {
            var id = String(data, "device");
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            return new GoveeLanDevice { DeviceId = id, Sku = String(data, "sku") ?? "light", Ip = String(data, "ip") ?? from.Address.ToString() };
        }

        public static GoveeLanStatus ParseStatus(JsonElement data)
        {
            var kelvin = Int(data, "colorTemInKelvin");
            return new GoveeLanStatus
            {
                On = Int(data, "onOff") == 1,
                Brightness = Int(data, "brightness"),
                ColorTempK = kelvin.HasValue && kelvin.Value > 0 ? kelvin : null,
            };
        }

        private static byte[] Message(string cmd, object data) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { msg = new { cmd, data } }));

        private static string String(JsonElement data, string name)
        {
            JsonElement value;
            return data.ValueKind == JsonValueKind.Object && data.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }

        private static int? Int(JsonElement data, string name)
        {
            JsonElement value;
            int number;
            return data.ValueKind == JsonValueKind.Object && data.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out number)
                ? number
                : (int?)null;
        }
    }

    /// <summary>
    /// A Govee light on the local network (DriverAddress = its IP). Fast and free, but only for models with LAN Control.
    /// PARTLY VERIFIED: scan and devStatus worked against three H6008 bulbs (2026-10-27); turn/brightness/colorwc are
    /// written from Govee's published WLAN guide and tested only against a fake transport.
    /// </summary>
    public sealed class GoveeLanDriver : IDeviceDriver
    {
        public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

        private readonly GoveeLanClient _client;
        private readonly TimeSpan _gapBetweenMessages;

        /// <param name="gapBetweenMessages">Pause between datagrams of one command; lights drop messages that arrive back to back.</param>
        public GoveeLanDriver(GoveeLanClient client, TimeSpan? gapBetweenMessages = null)
        {
            _client = client;
            _gapBetweenMessages = gapBetweenMessages ?? TimeSpan.FromMilliseconds(100);
        }

        public string Key => DriverKeys.GoveeLan;

        public TimeSpan? PollInterval => TimeSpan.FromSeconds(30);

        public async Task<DeviceState> ApplyAsync(Device device, DeviceCommand command)
        {
            var messages = new List<byte[]>();
            if (command.On == true || command.Brightness.HasValue) messages.Add(GoveeLanMessages.Turn(true));
            if (command.Brightness.HasValue) messages.Add(GoveeLanMessages.Brightness(command.Brightness.Value));
            if (command.ColorTempK.HasValue) messages.Add(GoveeLanMessages.ColorTemperature(command.ColorTempK.Value));
            if (command.On == false) messages.Add(GoveeLanMessages.Turn(false));

            try
            {
                for (var i = 0; i < messages.Count; i++)
                {
                    if (i > 0 && _gapBetweenMessages > TimeSpan.Zero) await Task.Delay(_gapBetweenMessages).ConfigureAwait(false);
                    await _client.SendAsync(device.DriverAddress, messages[i]).ConfigureAwait(false);
                }
            }
            catch (GoveeLanUnavailableException ex)
            {
                throw new DeviceOfflineException(device.Name, ex.Message);
            }
            catch (SocketException ex)
            {
                throw new DeviceOfflineException(device.Name, ex.Message);
            }

            // The light may report its old state for a moment, so what was just asked for wins.
            var state = await ReadAsync(device).ConfigureAwait(false);
            var desired = DeviceCommands.Apply(device, command);
            state.On = desired.On;
            if (command.Brightness.HasValue) state.Brightness = command.Brightness;
            if (command.ColorTempK.HasValue) state.ColorTempK = command.ColorTempK;
            return state;
        }

        public async Task<DeviceState> ReadAsync(Device device)
        {
            GoveeLanStatus status;
            try
            {
                status = await _client.StatusAsync(device.DriverAddress, Timeout).ConfigureAwait(false);
            }
            catch (GoveeLanUnavailableException ex)
            {
                throw new DeviceOfflineException(device.Name, ex.Message);
            }

            if (status == null)
            {
                throw new DeviceOfflineException(device.Name, "no response within " + Timeout.TotalSeconds + " s");
            }

            return new DeviceState { On = status.On, Brightness = status.Brightness, ColorTempK = status.ColorTempK };
        }
    }

    public sealed class GoveeLanDiscovery : IDeviceDiscovery
    {
        public static readonly TimeSpan ListenFor = TimeSpan.FromSeconds(3);

        private readonly GoveeLanClient _client;
        private readonly TimeSpan _listenFor;

        public GoveeLanDiscovery(GoveeLanClient client, TimeSpan? listenFor = null)
        {
            _client = client;
            _listenFor = listenFor ?? ListenFor;
        }

        public async Task<DiscoveryResult> DiscoverAsync(CancellationToken cancellationToken)
        {
            var result = new DiscoveryResult();
            IReadOnlyList<GoveeLanDevice> found;
            try
            {
                found = await _client.ScanAsync(_listenFor).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is GoveeLanUnavailableException || ex is SocketException)
            {
                result.Notes.Add("Govee local network search failed: " + ex.Message);
                return result;
            }

            foreach (var light in found)
            {
                result.Devices.Add(new DiscoveredDevice
                {
                    HardwareId = light.DeviceId,
                    Name = "Govee " + light.Sku,
                    Type = DeviceType.Light,
                    Driver = DriverKeys.GoveeLan,
                    Address = light.Ip,
                });
            }

            if (found.Count == 0)
            {
                result.Notes.Add("No Govee lights answered on the local network. In the Govee Home app, open each light's settings and turn on LAN Control (not every model has it).");
            }

            return result;
        }
    }
}
