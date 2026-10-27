using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Homebase.Domain;
using Homebase.Domain.Abstractions;
using Homebase.Domain.Devices;

namespace Homebase.Devices.AndroidTv
{
    /// <summary>
    /// Watches an Android / Google TV (DriverAddress = its IP or host name) through the Android TV Remote Service:
    /// pair once with the code the TV shows, then the TV pushes whether its screen is on. Read-only: Homebase never
    /// turns the TV on or off (the protocol only offers a power toggle, which could switch it on by mistake).
    /// UNVERIFIED AGAINST REAL HARDWARE: written from the community-documented protocol; pairing and power reporting are
    /// tested against message fixtures only.
    /// </summary>
    public sealed class AndroidTvDriver : IPairableDriver, IDisposable
    {
        public static readonly TimeSpan ReadWait = TimeSpan.FromSeconds(4);

        /// <summary>A started pairing is abandoned after this long (the TV hides the code much sooner).</summary>
        public static readonly TimeSpan PairingExpiry = TimeSpan.FromMinutes(3);

        private readonly AndroidTvCertificateStore _certificates;
        private readonly ConcurrentDictionary<string, AndroidTvConnection> _connections = new ConcurrentDictionary<string, AndroidTvConnection>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, AndroidTvPairingSession> _pairings = new ConcurrentDictionary<string, AndroidTvPairingSession>(StringComparer.Ordinal);

        public AndroidTvDriver(ISettingsRepository settings)
        {
            _certificates = new AndroidTvCertificateStore(settings);
        }

        public string Key => DriverKeys.AndroidTv;

        public TimeSpan? PollInterval => TimeSpan.FromSeconds(15);

        public Task<DeviceState> ApplyAsync(Device device, DeviceCommand command)
        {
            throw new DomainException(ErrorCodes.ReadOnlyDevice, device.Name + " is watched, not controlled.");
        }

        public async Task<DeviceState> ReadAsync(Device device)
        {
            var thumbprint = await _certificates.ServerThumbprintAsync(device.Id).ConfigureAwait(false);
            if (thumbprint == null)
            {
                throw new DeviceOfflineException(device.Name, "not paired yet (pair it on the Presence page)");
            }

            if (string.IsNullOrWhiteSpace(device.DriverAddress))
            {
                throw new DeviceOfflineException(device.Name, "no address");
            }

            // The thumbprint marks the TV as paired. It is not pinned on the remote port: the TV may present a different
            // certificate there, and a spoofed power state could only switch lights.
            var client = await _certificates.GetOrCreateAsync().ConfigureAwait(false);
            var connection = _connections.AddOrUpdate(
                device.Id,
                _ => new AndroidTvConnection(device.DriverAddress, client),
                (_, existing) =>
                {
                    if (existing.Host == device.DriverAddress) return existing;
                    existing.Dispose();
                    return new AndroidTvConnection(device.DriverAddress, client);
                });

            var on = await connection.ReadPowerAsync(ReadWait).ConfigureAwait(false);
            if (!on.HasValue)
            {
                throw new DeviceOfflineException(device.Name, connection.LastError ?? "no answer from the TV");
            }

            return new DeviceState { On = on };
        }

        public async Task<bool> IsPairedAsync(Device device) => await _certificates.ServerThumbprintAsync(device.Id).ConfigureAwait(false) != null;

        public async Task StartPairingAsync(Device device)
        {
            if (string.IsNullOrWhiteSpace(device.DriverAddress))
            {
                throw new DomainException(ErrorCodes.ValidationFailed, "Enter the TV's IP address first (TV Settings → Network).");
            }

            var client = await _certificates.GetOrCreateAsync().ConfigureAwait(false);
            var session = await AndroidTvPairingSession.StartAsync(device.DriverAddress.Trim(), client, "Homebase").ConfigureAwait(false);
            AndroidTvPairingSession previous;
            if (_pairings.TryRemove(device.Id, out previous)) previous.Dispose();
            _pairings[device.Id] = session;
        }

        public async Task<DeviceState> ConfirmPairingAsync(Device device, string code)
        {
            AndroidTvPairingSession session;
            if (!_pairings.TryGetValue(device.Id, out session) || DateTime.UtcNow - session.StartedAt > PairingExpiry)
            {
                throw new DomainException(ErrorCodes.PairingFailed, "No pairing in progress (or it expired). Start pairing again.");
            }

            await session.FinishAsync(code).ConfigureAwait(false);
            await _certificates.SetServerThumbprintAsync(device.Id, session.ServerThumbprint).ConfigureAwait(false);
            if (_pairings.TryRemove(device.Id, out session)) session.Dispose();

            AndroidTvConnection stale;
            if (_connections.TryRemove(device.Id, out stale)) stale.Dispose();
            return await ReadAsync(device).ConfigureAwait(false);
        }

        public void Dispose()
        {
            foreach (var connection in _connections.Values) connection.Dispose();
            foreach (var session in _pairings.Values) session.Dispose();
        }
    }
}
