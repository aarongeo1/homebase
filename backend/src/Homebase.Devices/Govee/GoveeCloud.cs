using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Homebase.Domain;
using Homebase.Domain.Devices;

namespace Homebase.Devices.Govee
{
    public sealed class GoveeCloudException : Exception
    {
        public GoveeCloudException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Govee's Developer API (openapi.api.govee.com/router/api/v1) with the key from the Govee Home app
    /// (Profile → Settings → Apply for API Key). Rate-limited per account per day, so cloud lights are polled rarely.
    /// </summary>
    public sealed class GoveeCloudClient
    {
        public const string ApiKeyHeader = "Govee-API-Key";
        public static readonly Uri BaseUri = new Uri("https://openapi.api.govee.com/router/api/v1/");
        public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);

        private readonly HttpClient _http;
        private readonly string _apiKey;

        public GoveeCloudClient(HttpClient http, string apiKey)
        {
            _http = http;
            _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        }

        public bool HasApiKey => _apiKey != null;

        public Task<JsonElement> GetAsync(string path, CancellationToken cancellationToken) =>
            SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(BaseUri, path)), cancellationToken);

        public Task<JsonElement> PostAsync(string path, object body, CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(BaseUri, path))
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            };
            return SendAsync(request, cancellationToken);
        }

        /// <summary>{"requestId", "payload": {"sku", "device", "capability": {"type", "instance", "value"}}}.</summary>
        public Task<JsonElement> ControlAsync(string sku, string device, string type, string instance, int value) =>
            PostAsync("device/control", new { requestId = Guid.NewGuid().ToString("N"), payload = new { sku, device, capability = new { type, instance, value } } }, CancellationToken.None);

        public Task<JsonElement> StateAsync(string sku, string device) =>
            PostAsync("device/state", new { requestId = Guid.NewGuid().ToString("N"), payload = new { sku, device } }, CancellationToken.None);

        private async Task<JsonElement> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_apiKey == null)
            {
                throw new GoveeCloudException("no Govee API key (set HOMEBASE_GOVEE_API_KEY)");
            }

            request.Headers.Add(ApiKeyHeader, _apiKey);
            using (request)
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeout.CancelAfter(Timeout);
                try
                {
                    using (var response = await _http.SendAsync(request, timeout.Token).ConfigureAwait(false))
                    {
                        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                        {
                            throw new GoveeCloudException("Govee rejected the API key");
                        }

                        if ((int)response.StatusCode == 429)
                        {
                            throw new GoveeCloudException("Govee cloud rate limit reached; try again later");
                        }

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new GoveeCloudException("Govee cloud returned HTTP " + (int)response.StatusCode);
                        }

                        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        using (var doc = JsonDocument.Parse(body))
                        {
                            JsonElement code;
                            if (doc.RootElement.TryGetProperty("code", out code) && code.ValueKind == JsonValueKind.Number && code.GetInt32() != 200)
                            {
                                JsonElement message;
                                var text = doc.RootElement.TryGetProperty("msg", out message) || doc.RootElement.TryGetProperty("message", out message) ? message.ToString() : "error";
                                throw new GoveeCloudException("Govee cloud said " + code.GetInt32() + " " + text);
                            }

                            return doc.RootElement.Clone();
                        }
                    }
                }
                catch (HttpRequestException ex)
                {
                    throw new GoveeCloudException(ex.Message);
                }
                catch (TaskCanceledException)
                {
                    throw new GoveeCloudException("no response from Govee cloud within " + Timeout.TotalSeconds + " s");
                }
                catch (JsonException)
                {
                    throw new GoveeCloudException("Govee cloud response was not JSON");
                }
            }
        }
    }

    /// <summary>Capability names in Govee's Developer API.</summary>
    public static class GoveeCapabilities
    {
        public const string OnOff = "devices.capabilities.on_off";
        public const string Range = "devices.capabilities.range";
        public const string ColorSetting = "devices.capabilities.color_setting";
        public const string Online = "devices.capabilities.online";

        public const string PowerSwitch = "powerSwitch";
        public const string Brightness = "brightness";
        public const string ColorTemperatureK = "colorTemperatureK";
        public const string OnlineInstance = "online";

        public const string LightType = "devices.types.light";
    }

    /// <summary>
    /// A Govee light through Govee's cloud (DriverAddress = "SKU|device"). Slower than LAN and rate-limited, so only used
    /// for lights that do not answer locally.
    /// UNVERIFIED AGAINST REAL HARDWARE: written from Govee's published Developer API docs and tested against a fake HTTP handler.
    /// </summary>
    public sealed class GoveeCloudDriver : IDeviceDriver
    {
        public const char AddressSeparator = '|';

        private readonly GoveeCloudClient _client;

        public GoveeCloudDriver(GoveeCloudClient client)
        {
            _client = client;
        }

        public string Key => DriverKeys.GoveeCloud;

        // Two lights polled every 15 s would use ~11,500 requests a day; every 5 minutes uses ~576.
        public TimeSpan? PollInterval => TimeSpan.FromMinutes(5);

        public static string Address(string sku, string device) => sku + AddressSeparator + device;

        public async Task<DeviceState> ApplyAsync(Device device, DeviceCommand command)
        {
            var address = Parse(device);
            try
            {
                if (command.On == true || command.Brightness.HasValue)
                {
                    await _client.ControlAsync(address.Item1, address.Item2, GoveeCapabilities.OnOff, GoveeCapabilities.PowerSwitch, 1).ConfigureAwait(false);
                }

                if (command.Brightness.HasValue)
                {
                    await _client.ControlAsync(address.Item1, address.Item2, GoveeCapabilities.Range, GoveeCapabilities.Brightness, command.Brightness.Value).ConfigureAwait(false);
                }

                if (command.ColorTempK.HasValue)
                {
                    await _client.ControlAsync(address.Item1, address.Item2, GoveeCapabilities.ColorSetting, GoveeCapabilities.ColorTemperatureK, command.ColorTempK.Value).ConfigureAwait(false);
                }

                if (command.On == false)
                {
                    await _client.ControlAsync(address.Item1, address.Item2, GoveeCapabilities.OnOff, GoveeCapabilities.PowerSwitch, 0).ConfigureAwait(false);
                }
            }
            catch (GoveeCloudException ex)
            {
                throw new DeviceOfflineException(device.Name, ex.Message);
            }

            // Govee confirmed each control call; reading back would spend more of the daily quota.
            return DeviceCommands.Apply(device, command);
        }

        public async Task<DeviceState> ReadAsync(Device device)
        {
            var address = Parse(device);
            JsonElement response;
            try
            {
                response = await _client.StateAsync(address.Item1, address.Item2).ConfigureAwait(false);
            }
            catch (GoveeCloudException ex)
            {
                throw new DeviceOfflineException(device.Name, ex.Message);
            }

            var values = CapabilityValues(response);
            JsonElement online;
            if (values.TryGetValue(GoveeCapabilities.OnlineInstance, out online) && online.ValueKind == JsonValueKind.False)
            {
                throw new DeviceOfflineException(device.Name, "Govee cloud says it is offline");
            }

            var state = device.State.Clone();
            JsonElement value;
            if (values.TryGetValue(GoveeCapabilities.PowerSwitch, out value) && value.ValueKind == JsonValueKind.Number) state.On = value.GetInt32() == 1;
            if (values.TryGetValue(GoveeCapabilities.Brightness, out value) && value.ValueKind == JsonValueKind.Number) state.Brightness = value.GetInt32();
            if (values.TryGetValue(GoveeCapabilities.ColorTemperatureK, out value) && value.ValueKind == JsonValueKind.Number)
            {
                var kelvin = value.GetInt32();
                state.ColorTempK = kelvin > 0 ? kelvin : (int?)null;
            }

            return state;
        }

        /// <summary>payload.capabilities[] → instance → state.value.</summary>
        internal static Dictionary<string, JsonElement> CapabilityValues(JsonElement response)
        {
            var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            JsonElement payload;
            JsonElement capabilities;
            if (!response.TryGetProperty("payload", out payload) || !payload.TryGetProperty("capabilities", out capabilities) || capabilities.ValueKind != JsonValueKind.Array)
            {
                return values;
            }

            foreach (var capability in capabilities.EnumerateArray())
            {
                JsonElement instance;
                JsonElement state;
                JsonElement value;
                if (capability.TryGetProperty("instance", out instance) && instance.ValueKind == JsonValueKind.String
                    && capability.TryGetProperty("state", out state) && state.ValueKind == JsonValueKind.Object && state.TryGetProperty("value", out value))
                {
                    values[instance.GetString()] = value.Clone();
                }
            }

            return values;
        }

        private static Tuple<string, string> Parse(Device device)
        {
            var parts = (device.DriverAddress ?? string.Empty).Split(new[] { AddressSeparator }, 2);
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            {
                throw new DeviceOfflineException(device.Name, "its Govee cloud address is not 'SKU|device'");
            }

            return Tuple.Create(parts[0], parts[1]);
        }
    }

    public sealed class GoveeCloudDiscovery : IDeviceDiscovery
    {
        private readonly GoveeCloudClient _client;

        public GoveeCloudDiscovery(GoveeCloudClient client)
        {
            _client = client;
        }

        public async Task<DiscoveryResult> DiscoverAsync(CancellationToken cancellationToken)
        {
            var result = new DiscoveryResult();
            if (!_client.HasApiKey)
            {
                result.Notes.Add("Govee cloud search skipped: set HOMEBASE_GOVEE_API_KEY on the Mac (Govee Home app → Profile → Settings → Apply for API Key).");
                return result;
            }

            JsonElement response;
            try
            {
                response = await _client.GetAsync("user/devices", cancellationToken).ConfigureAwait(false);
            }
            catch (GoveeCloudException ex)
            {
                result.Notes.Add("Govee cloud search failed: " + ex.Message + ".");
                return result;
            }

            JsonElement data;
            if (!response.TryGetProperty("data", out data) || data.ValueKind != JsonValueKind.Array)
            {
                result.Notes.Add("Govee cloud returned no device list.");
                return result;
            }

            foreach (var item in data.EnumerateArray())
            {
                var sku = Text(item, "sku");
                var device = Text(item, "device");
                if (sku == null || device == null || Text(item, "type") != GoveeCapabilities.LightType)
                {
                    continue;
                }

                result.Devices.Add(new DiscoveredDevice
                {
                    HardwareId = device,
                    Name = Text(item, "deviceName") ?? "Govee " + sku,
                    Type = DeviceType.Light,
                    Driver = DriverKeys.GoveeCloud,
                    Address = GoveeCloudDriver.Address(sku, device),
                });
            }

            return result;
        }

        private static string Text(JsonElement item, string name)
        {
            JsonElement value;
            return item.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }
    }
}
