using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Homebase.Domain.Abstractions;

namespace Homebase.Devices.AndroidTv
{
    /// <summary>One TLS connection to the TV with our client certificate. The TV's certificate is self-signed.</summary>
    internal sealed class AndroidTvTlsConnection : IDisposable
    {
        private readonly TcpClient _tcp;

        private AndroidTvTlsConnection(TcpClient tcp, SslStream stream, X509Certificate2 server)
        {
            _tcp = tcp;
            Stream = stream;
            ServerCertificate = server;
        }

        public SslStream Stream { get; }

        public X509Certificate2 ServerCertificate { get; }

        /// <param name="expectedThumbprint">Only accept a server certificate with this thumbprint; null accepts the TV's self-signed one.</param>
        public static async Task<AndroidTvTlsConnection> OpenAsync(string host, int port, X509Certificate2 clientCertificate, string expectedThumbprint, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var tcp = new TcpClient();
            try
            {
                using (var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    connectTimeout.CancelAfter(timeout);
                    await tcp.ConnectAsync(host, port, connectTimeout.Token).ConfigureAwait(false);
                    var ssl = new SslStream(tcp.GetStream(), false);
                    await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                    {
                        TargetHost = host,
                        ClientCertificates = new X509CertificateCollection { clientCertificate },
                        EnabledSslProtocols = SslProtocols.None,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                        RemoteCertificateValidationCallback = (sender, certificate, chain, errors) =>
                            certificate != null && (expectedThumbprint == null
                                || string.Equals(new X509Certificate2(certificate).Thumbprint, expectedThumbprint, StringComparison.OrdinalIgnoreCase)),
                    }, connectTimeout.Token).ConfigureAwait(false);

                    return new AndroidTvTlsConnection(tcp, ssl, new X509Certificate2(ssl.RemoteCertificate));
                }
            }
            catch
            {
                tcp.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            Stream.Dispose();
            _tcp.Dispose();
        }
    }

    /// <summary>Our one client certificate for every TV, created on first use and kept in the settings table.</summary>
    internal sealed class AndroidTvCertificateStore
    {
        public const string CertificateKey = "androidtv.client-certificate";

        private readonly ISettingsRepository _settings;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private X509Certificate2 _certificate;

        public AndroidTvCertificateStore(ISettingsRepository settings)
        {
            _settings = settings;
        }

        public static string PairedKey(string deviceId) => "androidtv.server-thumbprint." + deviceId;

        public async Task<X509Certificate2> GetOrCreateAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_certificate != null)
                {
                    return _certificate;
                }

                var stored = await _settings.GetAsync(CertificateKey).ConfigureAwait(false);
                byte[] pfx;
                if (stored == null)
                {
                    pfx = Create();
                    await _settings.SetAsync(CertificateKey, Convert.ToBase64String(pfx)).ConfigureAwait(false);
                }
                else
                {
                    pfx = Convert.FromBase64String(stored);
                }

                _certificate = X509CertificateLoader.LoadPkcs12(pfx, null);
                return _certificate;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>The TV's certificate thumbprint recorded at pairing; null if never paired.</summary>
        public Task<string> ServerThumbprintAsync(string deviceId) => _settings.GetAsync(PairedKey(deviceId));

        public Task SetServerThumbprintAsync(string deviceId, string thumbprint) => _settings.SetAsync(PairedKey(deviceId), thumbprint);

        private static byte[] Create()
        {
            using (var rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest("CN=Homebase", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                using (var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(20)))
                {
                    return certificate.Export(X509ContentType.Pkcs12);
                }
            }
        }
    }
}
