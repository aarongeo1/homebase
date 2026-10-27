using System;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Homebase.Devices.AndroidTv;
using Homebase.Domain;
using Homebase.Domain.Abstractions;
using Homebase.Domain.Devices;
using Homebase.Testing;
using Xunit;

namespace Homebase.Devices.Tests
{
    public class AndroidTvTests
    {
        private static readonly byte[] ClientModulus = new[] { (byte)0xC0 }.Concat(Enumerable.Range(1, 255).Select(i => (byte)(i % 256))).ToArray();
        private static readonly byte[] ServerModulus = new[] { (byte)0xD1 }.Concat(Enumerable.Range(1, 255).Select(i => (byte)((i * 7) % 256))).ToArray();
        private static readonly byte[] Exponent = { 0x01, 0x00, 0x01 };

        [Fact]
        public void PairingSecret_MatchesAnIndependentImplementation()
        {
            // Expected values computed with Python the way the androidtvremote2 client library does it.
            var secret = AndroidTvMessages.PairingSecretFor(
                new RSAParameters { Modulus = ClientModulus, Exponent = Exponent },
                new RSAParameters { Modulus = ServerModulus, Exponent = Exponent },
                "9D1234");

            Assert.Equal("9d1a227b4b4a350479601231f998ba195deea4e4dbe3b7d10ab08cc4f1928137", Convert.ToHexString(secret).ToLowerInvariant());
        }

        [Fact]
        public void PairingSecret_RejectsAMistypedCode()
        {
            Assert.Null(AndroidTvMessages.PairingSecretFor(
                new RSAParameters { Modulus = ClientModulus, Exponent = Exponent },
                new RSAParameters { Modulus = ServerModulus, Exponent = Exponent },
                "9E1234"));
            Assert.False(AndroidTvMessages.IsCode("12345"));
            Assert.False(AndroidTvMessages.IsCode("12345G"));
        }

        [Fact]
        public void PairingRequest_IsProtobufWithVersionStatusAndNames()
        {
            var fields = ProtoReader.Parse(AndroidTvMessages.PairingRequest("atvremote", "Homebase"));
            Assert.Equal(2UL, fields.Find(1).Varint);
            Assert.Equal(200UL, fields.Find(2).Varint);
            var request = ProtoReader.Parse(fields.Find(10).Bytes);
            Assert.Equal("atvremote", System.Text.Encoding.UTF8.GetString(request.Find(1).Bytes));
            Assert.Equal("Homebase", System.Text.Encoding.UTF8.GetString(request.Find(2).Bytes));
        }

        [Fact]
        public void PairingOption_AsksForSixHexCharactersAsInput()
        {
            var option = ProtoReader.Parse(ProtoReader.Parse(AndroidTvMessages.PairingOption()).Find(20).Bytes);
            var encoding = ProtoReader.Parse(option.Find(1).Bytes);
            Assert.Equal(3UL, encoding.Find(1).Varint);
            Assert.Equal(6UL, encoding.Find(2).Varint);
            Assert.Equal(1UL, option.Find(3).Varint);
        }

        [Fact]
        public void Connection_AnswersConfigureActiveAndPings_AndTracksPower()
        {
            var connection = new AndroidTvConnection("tv", null);
            Assert.NotNull(ProtoReader.Parse(connection.Handle(new ProtoWriterFacade().Message(1, new ProtoWriterFacade().Varint(1, 639)).ToArray())).Find(1));
            Assert.NotNull(ProtoReader.Parse(connection.Handle(new ProtoWriterFacade().Message(2, new ProtoWriterFacade().Varint(1, 622)).ToArray())).Find(2));

            var pong = ProtoReader.Parse(connection.Handle(new ProtoWriterFacade().Message(8, new ProtoWriterFacade().Varint(1, 42).Varint(2, 7)).ToArray()));
            Assert.Equal(42UL, ProtoReader.Parse(pong.Find(9).Bytes).Find(1).Varint);

            Assert.Null(connection.Handle(new ProtoWriterFacade().Message(40, new ProtoWriterFacade().Bool(1, true)).ToArray()));
        }

        [Fact]
        public async Task Framing_RoundTripsThroughAStream()
        {
            var stream = new System.IO.MemoryStream();
            var big = Enumerable.Repeat((byte)7, 300).ToArray();
            await ProtoReader.WriteFramedAsync(stream, big, default);
            stream.Position = 0;
            Assert.Equal(big, await ProtoReader.ReadFramedAsync(stream, default));
            Assert.Null(await ProtoReader.ReadFramedAsync(stream, default));
        }

        [Fact]
        public async Task CertificateStore_CreatesOnceAndReloads()
        {
            var settings = new InMemoryHome();
            var first = await new AndroidTvCertificateStore(settings).GetOrCreateAsync();
            var second = await new AndroidTvCertificateStore(settings).GetOrCreateAsync();

            Assert.True(first.HasPrivateKey);
            Assert.Equal(first.Thumbprint, second.Thumbprint);
            Assert.Equal(256, first.GetRSAPublicKey().ExportParameters(false).Modulus.Length);
        }

        /// <summary>Proves the client certificate works for TLS on this OS (macOS keychain quirks included), against a local server.</summary>
        [Fact]
        public async Task TlsConnection_PresentsTheClientCertificate()
        {
            var settings = new InMemoryHome();
            var client = await new AndroidTvCertificateStore(settings).GetOrCreateAsync();
            using (var rsa = RSA.Create(2048))
            using (var server = X509CertificateLoader.LoadPkcs12(new CertificateRequest("CN=tv", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1)).Export(X509ContentType.Pkcs12), null))
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                var seen = Task.Run(async () =>
                {
                    using (var tcp = await listener.AcceptTcpClientAsync())
                    using (var ssl = new SslStream(tcp.GetStream(), false))
                    {
                        await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = server, ClientCertificateRequired = true, RemoteCertificateValidationCallback = (s, c, ch, e) => true });
                        return new X509Certificate2(ssl.RemoteCertificate).Thumbprint;
                    }
                });

                var opening = AndroidTvTlsConnection.OpenAsync("127.0.0.1", port, client, null, TimeSpan.FromSeconds(10), default);
                Assert.Equal(client.Thumbprint, await seen);
                using (var connection = await opening)
                {
                    Assert.Equal(server.Thumbprint, connection.ServerCertificate.Thumbprint);
                }

                listener.Stop();
            }
        }

        [Fact]
        public async Task UnpairedTv_IsOfflineWithAHint()
        {
            var driver = new AndroidTvDriver(new InMemoryHome());
            var tv = new Device { Id = "tv", Name = "Dining room TV", Type = DeviceType.Tv, Driver = DriverKeys.AndroidTv, DriverAddress = "192.0.2.1" };
            Assert.False(await driver.IsPairedAsync(tv));
            var ex = await Assert.ThrowsAsync<DeviceOfflineException>(() => driver.ReadAsync(tv));
            Assert.Contains("not paired", ex.Message);
        }

        [Fact]
        public async Task ConfirmWithoutStart_FailsClearly()
        {
            var driver = new AndroidTvDriver(new InMemoryHome());
            var tv = new Device { Id = "tv", Name = "Dining room TV", Type = DeviceType.Tv, Driver = DriverKeys.AndroidTv, DriverAddress = "192.0.2.1" };
            var ex = await Assert.ThrowsAsync<DomainException>(() => driver.ConfirmPairingAsync(tv, "ABC123"));
            Assert.Equal(ErrorCodes.PairingFailed, ex.Code);
        }

        /// <summary>The writer is internal to the driver assembly; this keeps the tests readable.</summary>
        private sealed class ProtoWriterFacade
        {
            private readonly ProtoWriter _writer = new ProtoWriter();

            public ProtoWriterFacade Varint(int field, long value)
            {
                _writer.Varint(field, value);
                return this;
            }

            public ProtoWriterFacade Bool(int field, bool value)
            {
                _writer.Bool(field, value);
                return this;
            }

            public ProtoWriterFacade Message(int field, ProtoWriterFacade inner)
            {
                _writer.Message(field, inner._writer);
                return this;
            }

            public byte[] ToArray() => _writer.ToArray();
        }
    }
}
