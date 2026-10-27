using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Homebase.Devices.AndroidTv
{
    /// <summary>
    /// Just enough protocol buffers for the Android TV remote protocol: varints and length-delimited fields.
    /// Hand-written to avoid a code generator for a dozen small messages.
    /// </summary>
    internal sealed class ProtoWriter
    {
        private readonly MemoryStream _buffer = new MemoryStream();

        public ProtoWriter Varint(int field, long value)
        {
            Tag(field, 0);
            WriteVarint(_buffer, (ulong)value);
            return this;
        }

        public ProtoWriter Bool(int field, bool value) => Varint(field, value ? 1 : 0);

        public ProtoWriter String(int field, string value) => Bytes(field, Encoding.UTF8.GetBytes(value ?? string.Empty));

        public ProtoWriter Bytes(int field, byte[] value)
        {
            Tag(field, 2);
            WriteVarint(_buffer, (ulong)value.Length);
            _buffer.Write(value, 0, value.Length);
            return this;
        }

        public ProtoWriter Message(int field, ProtoWriter inner) => Bytes(field, inner.ToArray());

        public byte[] ToArray() => _buffer.ToArray();

        internal static void WriteVarint(Stream stream, ulong value)
        {
            while (value >= 0x80)
            {
                stream.WriteByte((byte)(value | 0x80));
                value >>= 7;
            }

            stream.WriteByte((byte)value);
        }

        private void Tag(int field, int wireType) => WriteVarint(_buffer, (ulong)((field << 3) | wireType));
    }

    internal sealed class ProtoField
    {
        public int Number { get; set; }

        public ulong Varint { get; set; }

        /// <summary>Null for varint fields.</summary>
        public byte[] Bytes { get; set; }
    }

    internal static class ProtoReader
    {
        public static List<ProtoField> Parse(byte[] data)
        {
            var fields = new List<ProtoField>();
            var position = 0;
            while (position < data.Length)
            {
                var tag = ReadVarint(data, ref position);
                var field = new ProtoField { Number = (int)(tag >> 3) };
                switch ((int)(tag & 7))
                {
                    case 0:
                        field.Varint = ReadVarint(data, ref position);
                        break;
                    case 2:
                        var length = (int)ReadVarint(data, ref position);
                        if (length < 0 || position + length > data.Length) throw new InvalidDataException("Truncated protobuf field.");
                        field.Bytes = data.Skip(position).Take(length).ToArray();
                        position += length;
                        break;
                    case 5:
                        position += 4;
                        break;
                    case 1:
                        position += 8;
                        break;
                    default:
                        throw new InvalidDataException("Unsupported protobuf wire type " + (tag & 7) + ".");
                }

                fields.Add(field);
            }

            return fields;
        }

        public static ProtoField Find(this IEnumerable<ProtoField> fields, int number) => fields.FirstOrDefault(f => f.Number == number);

        /// <summary>Reads one message prefixed by its varint length; null at end of stream.</summary>
        public static async Task<byte[]> ReadFramedAsync(Stream stream, CancellationToken cancellationToken)
        {
            ulong length = 0;
            var shift = 0;
            var one = new byte[1];
            while (true)
            {
                if (await stream.ReadAsync(one, 0, 1, cancellationToken).ConfigureAwait(false) == 0)
                {
                    return null;
                }

                length |= (ulong)(one[0] & 0x7F) << shift;
                if ((one[0] & 0x80) == 0) break;
                shift += 7;
                if (shift > 28) throw new InvalidDataException("Message length is too large.");
            }

            var message = new byte[length];
            var read = 0;
            while (read < message.Length)
            {
                var n = await stream.ReadAsync(message, read, message.Length - read, cancellationToken).ConfigureAwait(false);
                if (n == 0) return null;
                read += n;
            }

            return message;
        }

        public static async Task WriteFramedAsync(Stream stream, byte[] message, CancellationToken cancellationToken)
        {
            var framed = new MemoryStream();
            ProtoWriter.WriteVarint(framed, (ulong)message.Length);
            framed.Write(message, 0, message.Length);
            var bytes = framed.ToArray();
            await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private static ulong ReadVarint(byte[] data, ref int position)
        {
            ulong value = 0;
            var shift = 0;
            while (true)
            {
                if (position >= data.Length) throw new InvalidDataException("Truncated varint.");
                var b = data[position++];
                value |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return value;
                shift += 7;
                if (shift > 63) throw new InvalidDataException("Varint is too long.");
            }
        }
    }
}
