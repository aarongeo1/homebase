using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Homebase.Devices.AndroidTv
{
    /// <summary>
    /// A long-lived remote connection on TCP 6466. The TV pushes "remote_start" (screen on/off) and pings every few
    /// seconds; it drops clients that stop answering. Reconnects with back-off while the driver keeps asking.
    /// </summary>
    internal sealed class AndroidTvConnection : IDisposable
    {
        public const int Port = 6466;
        public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(1);

        private readonly X509Certificate2 _client;
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private readonly object _gate = new object();
        private Task _loop;
        private volatile bool _connected;
        private bool? _on;

        public AndroidTvConnection(string host, X509Certificate2 client)
        {
            Host = host;
            _client = client;
        }

        public string Host { get; }

        public string LastError { get; private set; }

        /// <summary>Waits up to <paramref name="wait"/> for a connection that has reported power; null if there is none.</summary>
        public async Task<bool?> ReadPowerAsync(TimeSpan wait)
        {
            lock (_gate)
            {
                if (_loop == null) _loop = Task.Run(RunAsync);
            }

            var deadline = DateTime.UtcNow + wait;
            while (true)
            {
                bool? on;
                lock (_gate)
                {
                    on = _connected ? _on : null;
                }

                if (on.HasValue || DateTime.UtcNow >= deadline)
                {
                    return on;
                }

                await Task.Delay(100).ConfigureAwait(false);
            }
        }

        public void Dispose() => _stop.Cancel();

        private async Task RunAsync()
        {
            var backoff = TimeSpan.FromSeconds(2);
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    using (var connection = await AndroidTvTlsConnection.OpenAsync(Host, Port, _client, null, ConnectTimeout, _stop.Token).ConfigureAwait(false))
                    {
                        _connected = true;
                        backoff = TimeSpan.FromSeconds(2);
                        await ReadLoopAsync(connection).ConfigureAwait(false);
                    }

                    LastError = "the TV closed the connection";
                }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex) when (!(ex is OutOfMemoryException))
                {
                    LastError = ex.Message;
                }
                finally
                {
                    lock (_gate)
                    {
                        _connected = false;
                        _on = null;
                    }
                }

                try
                {
                    await Task.Delay(backoff, _stop.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, MaxBackoff.Ticks));
            }
        }

        private async Task ReadLoopAsync(AndroidTvTlsConnection connection)
        {
            while (!_stop.IsCancellationRequested)
            {
                var message = await ProtoReader.ReadFramedAsync(connection.Stream, _stop.Token).ConfigureAwait(false);
                if (message == null)
                {
                    return;
                }

                var reply = Handle(message);
                if (reply != null)
                {
                    await ProtoReader.WriteFramedAsync(connection.Stream, reply, _stop.Token).ConfigureAwait(false);
                }
            }
        }

        /// <summary>Updates power state and returns the reply the TV expects, if any.</summary>
        internal byte[] Handle(byte[] message)
        {
            var fields = ProtoReader.Parse(message);
            if (fields.Find(AndroidTvMessages.FieldRemoteConfigure) != null)
            {
                return AndroidTvMessages.RemoteConfigure("Homebase", "Homebase");
            }

            if (fields.Find(AndroidTvMessages.FieldRemoteSetActive) != null)
            {
                return AndroidTvMessages.RemoteSetActive();
            }

            var ping = fields.Find(AndroidTvMessages.FieldRemotePingRequest);
            if (ping != null)
            {
                var val1 = ProtoReader.Parse(ping.Bytes).Find(1);
                return AndroidTvMessages.RemotePingResponse(val1 == null ? 0 : (long)val1.Varint);
            }

            var start = fields.Find(AndroidTvMessages.FieldRemoteStart);
            if (start != null)
            {
                var started = ProtoReader.Parse(start.Bytes).Find(1);
                lock (_gate)
                {
                    _on = started != null && started.Varint != 0;
                }
            }

            var error = fields.Find(AndroidTvMessages.FieldRemoteError);
            if (error != null)
            {
                LastError = "the TV reported a remote error";
            }

            return null;
        }
    }
}
