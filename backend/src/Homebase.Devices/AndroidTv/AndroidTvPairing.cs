using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Homebase.Domain;

namespace Homebase.Devices.AndroidTv
{
    /// <summary>
    /// One pairing attempt on TCP 6467: request → options → configuration makes the TV show a 6-character code;
    /// the secret derived from that code and both certificates completes it. The connection stays open in between.
    /// </summary>
    internal sealed class AndroidTvPairingSession : IDisposable
    {
        public const int Port = 6467;
        public const string ServiceName = "atvremote";
        public static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(10);

        private readonly AndroidTvTlsConnection _connection;
        private readonly X509Certificate2 _client;

        private AndroidTvPairingSession(AndroidTvTlsConnection connection, X509Certificate2 client)
        {
            _connection = connection;
            _client = client;
            StartedAt = DateTime.UtcNow;
        }

        public DateTime StartedAt { get; }

        public string ServerThumbprint => _connection.ServerCertificate.Thumbprint;

        public static async Task<AndroidTvPairingSession> StartAsync(string host, X509Certificate2 client, string clientName)
        {
            AndroidTvTlsConnection connection;
            try
            {
                connection = await AndroidTvTlsConnection.OpenAsync(host, Port, client, null, StepTimeout, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                throw new DomainException(ErrorCodes.PairingFailed, "Could not reach the TV at " + host + ": " + ex.Message);
            }

            var session = new AndroidTvPairingSession(connection, client);
            try
            {
                await session.ExchangeAsync(AndroidTvMessages.PairingRequest(ServiceName, clientName), AndroidTvMessages.FieldPairingRequestAck).ConfigureAwait(false);
                await session.ExchangeAsync(AndroidTvMessages.PairingOption(), AndroidTvMessages.FieldPairingOption).ConfigureAwait(false);
                await session.ExchangeAsync(AndroidTvMessages.PairingConfiguration(), AndroidTvMessages.FieldPairingConfigurationAck).ConfigureAwait(false);
                return session;
            }
            catch
            {
                session.Dispose();
                throw;
            }
        }

        /// <summary>Throws pairing_failed for a mistyped code (the session stays usable) or when the TV rejects it.</summary>
        public async Task FinishAsync(string code)
        {
            if (!AndroidTvMessages.IsCode(code))
            {
                throw new DomainException(ErrorCodes.ValidationFailed, "The code is 6 characters, 0-9 and A-F.");
            }

            byte[] secret;
            using (var clientKey = _client.GetRSAPublicKey())
            using (var serverKey = _connection.ServerCertificate.GetRSAPublicKey())
            {
                if (clientKey == null || serverKey == null)
                {
                    throw new DomainException(ErrorCodes.PairingFailed, "The TV's certificate does not use RSA; this TV is not supported.");
                }

                secret = AndroidTvMessages.PairingSecretFor(clientKey.ExportParameters(false), serverKey.ExportParameters(false), code.ToUpperInvariant());
            }

            if (secret == null)
            {
                throw new DomainException(ErrorCodes.PairingFailed, "That code does not match the one on the TV. Check it and try again.");
            }

            await ExchangeAsync(AndroidTvMessages.PairingSecret(secret), AndroidTvMessages.FieldPairingSecretAck).ConfigureAwait(false);
        }

        public void Dispose() => _connection.Dispose();

        private async Task ExchangeAsync(byte[] request, int expectedField)
        {
            try
            {
                using (var timeout = new CancellationTokenSource(StepTimeout))
                {
                    await ProtoReader.WriteFramedAsync(_connection.Stream, request, timeout.Token).ConfigureAwait(false);
                    var reply = await ProtoReader.ReadFramedAsync(_connection.Stream, timeout.Token).ConfigureAwait(false);
                    if (reply == null)
                    {
                        throw new DomainException(ErrorCodes.PairingFailed, "The TV closed the connection. Start pairing again.");
                    }

                    var fields = ProtoReader.Parse(reply);
                    var status = AndroidTvMessages.Status(fields);
                    if (status != AndroidTvMessages.StatusOk)
                    {
                        throw new DomainException(ErrorCodes.PairingFailed, "The TV refused the pairing step (status " + status + ").");
                    }

                    if (fields.Find(expectedField) == null)
                    {
                        throw new DomainException(ErrorCodes.PairingFailed, "The TV sent an unexpected pairing message.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw new DomainException(ErrorCodes.PairingFailed, "The TV did not answer within " + StepTimeout.TotalSeconds + " s.");
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is System.IO.InvalidDataException)
            {
                throw new DomainException(ErrorCodes.PairingFailed, "Lost the connection to the TV: " + ex.Message);
            }
        }
    }
}
