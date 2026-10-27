using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace Homebase.Devices.AndroidTv
{
    /// <summary>
    /// The "Android TV Remote Service" v2 protocol used by Google's phone remote: pairing on TCP 6467 and remote
    /// control on TCP 6466, both TLS with a client certificate, protobuf messages framed by a varint length.
    /// Field numbers follow the community-documented pairingmessage.proto and remotemessage.proto.
    /// </summary>
    internal static class AndroidTvMessages
    {
        public const int ProtocolVersion = 2;
        public const int StatusOk = 200;
        public const int EncodingHexadecimal = 3;
        public const int RoleInput = 1;
        public const int CodeLength = 6;

        // PairingMessage fields
        public const int FieldProtocolVersion = 1;
        public const int FieldStatus = 2;
        public const int FieldPairingRequest = 10;
        public const int FieldPairingRequestAck = 11;
        public const int FieldPairingOption = 20;
        public const int FieldPairingConfiguration = 30;
        public const int FieldPairingConfigurationAck = 31;
        public const int FieldPairingSecret = 40;
        public const int FieldPairingSecretAck = 41;

        // RemoteMessage fields
        public const int FieldRemoteConfigure = 1;
        public const int FieldRemoteSetActive = 2;
        public const int FieldRemoteError = 3;
        public const int FieldRemotePingRequest = 8;
        public const int FieldRemotePingResponse = 9;
        public const int FieldRemoteStart = 40;

        /// <summary>Feature bits the client announces (the value Google's remote and common clients send).</summary>
        public const int RemoteFeatures = 622;

        public static byte[] PairingRequest(string serviceName, string clientName) =>
            Pairing().Message(FieldPairingRequest, new ProtoWriter().String(1, serviceName).String(2, clientName)).ToArray();

        /// <summary>We type the code (input role); the TV shows 6 hexadecimal characters.</summary>
        public static byte[] PairingOption() =>
            Pairing().Message(FieldPairingOption, new ProtoWriter().Message(1, HexEncoding()).Varint(3, RoleInput)).ToArray();

        public static byte[] PairingConfiguration() =>
            Pairing().Message(FieldPairingConfiguration, new ProtoWriter().Message(1, HexEncoding()).Varint(2, RoleInput)).ToArray();

        public static byte[] PairingSecret(byte[] secret) =>
            Pairing().Message(FieldPairingSecret, new ProtoWriter().Bytes(1, secret)).ToArray();

        public static byte[] RemoteConfigure(string model, string vendor) =>
            new ProtoWriter().Message(FieldRemoteConfigure, new ProtoWriter()
                .Varint(1, RemoteFeatures)
                .Message(2, new ProtoWriter().String(1, model).String(2, vendor).Varint(3, 1).String(4, "1").String(5, "homebase").String(6, "1.0.0")))
            .ToArray();

        public static byte[] RemoteSetActive() =>
            new ProtoWriter().Message(FieldRemoteSetActive, new ProtoWriter().Varint(1, RemoteFeatures)).ToArray();

        public static byte[] RemotePingResponse(long val1) =>
            new ProtoWriter().Message(FieldRemotePingResponse, new ProtoWriter().Varint(1, val1)).ToArray();

        /// <summary>
        /// SHA-256 over client modulus, client exponent, server modulus, server exponent (big-endian, no leading zero
        /// bytes) and the last 4 code characters as 2 bytes. The TV only accepts it if the first hash byte equals the
        /// first 2 code characters, which also tells us early that the user mistyped. Null when that check fails.
        /// </summary>
        public static byte[] PairingSecretFor(RSAParameters client, RSAParameters server, string code)
        {
            if (!IsCode(code))
            {
                throw new ArgumentException("The code must be 6 hexadecimal characters.", nameof(code));
            }

            var codeBytes = Convert.FromHexString(code);
            using (var sha = SHA256.Create())
            {
                var input = Strip(client.Modulus).Concat(Strip(client.Exponent)).Concat(Strip(server.Modulus)).Concat(Strip(server.Exponent))
                    .Concat(codeBytes.Skip(1)).ToArray();
                var hash = sha.ComputeHash(input);
                return hash[0] == codeBytes[0] ? hash : null;
            }
        }

        public static bool IsCode(string code) =>
            code != null && code.Length == CodeLength && code.All(Uri.IsHexDigit);

        public static int Status(List<ProtoField> message)
        {
            var status = message.Find(FieldStatus);
            return status == null ? 0 : (int)status.Varint;
        }

        private static ProtoWriter Pairing() => new ProtoWriter().Varint(FieldProtocolVersion, ProtocolVersion).Varint(FieldStatus, StatusOk);

        private static ProtoWriter HexEncoding() => new ProtoWriter().Varint(1, EncodingHexadecimal).Varint(2, CodeLength);

        private static IEnumerable<byte> Strip(byte[] bigEndian) => bigEndian.SkipWhile(b => b == 0);
    }
}
