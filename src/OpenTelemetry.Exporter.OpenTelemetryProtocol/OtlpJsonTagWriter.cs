// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol;

internal sealed class OtlpJsonTagWriter : JsonStringArrayTagWriter<Utf8JsonWriter>
{
    public static OtlpJsonTagWriter Instance { get; } = new();

    protected override void OnUnsupportedTagDropped(string tagKey, string tagValueTypeFullName)
        => throw new NotImplementedException();

    protected override void WriteArrayTag(ref Utf8JsonWriter writer, string key, ArraySegment<byte> arrayUtf8JsonBytes)
    {
        writer.WriteStartObject("arrayValue");
        writer.WritePropertyName("values");
        writer.WriteStringValue(arrayUtf8JsonBytes);
        writer.WriteEndObject();
    }

    protected override void WriteBooleanTag(ref Utf8JsonWriter state, string key, bool value)
        => state.WriteBoolean("boolValue", value);

    protected override void WriteFloatingPointTag(ref Utf8JsonWriter state, string key, double value)
        => state.WriteNumber("doubleValue", value);

    protected override void WriteIntegralTag(ref Utf8JsonWriter state, string key, long value)
        => state.WriteNumber("intValue", value);

    protected override void WriteStringTag(ref Utf8JsonWriter state, string key, ReadOnlySpan<char> value)
        => state.WriteString("stringValue", value);
}
