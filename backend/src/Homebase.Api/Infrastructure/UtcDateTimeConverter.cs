using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Homebase.Api.Infrastructure
{
    /// <summary>Every timestamp on the wire is UTC with a trailing Z, to the second (contracts/openapi.yaml conventions).</summary>
    public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
    {
        private const string Format = "yyyy-MM-dd'T'HH:mm:ss'Z'";

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            DateTime.Parse(reader.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            var utc = value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
            writer.WriteStringValue(utc.ToString(Format, CultureInfo.InvariantCulture));
        }
    }
}
