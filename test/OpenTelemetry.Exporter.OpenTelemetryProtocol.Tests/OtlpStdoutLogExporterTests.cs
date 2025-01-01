// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET8_0_OR_GREATER

using System.Text.Json;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpStdoutLogExporterTests
{
    [Fact]
    public void OtlpStdoutJsonTagWriter_TryWriteTag()
    {
        var stream = new MemoryStream();
        var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        var success = OtlpStdoutJsonTagWriter.Instance.TryWriteTag(ref writer, "key", "value");
        writer.WriteEndObject();
        writer.Flush();
        Assert.True(success);

        var doc = JsonDocument.Parse(stream.ToArray());
        var root = doc.RootElement;
        var value = root.GetProperty("stringValue");
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        Assert.Equal("value", value.GetString());
    }

    [Fact]
    public void OtlpStdoutJsonTagWriterExtensions_TryWriteAnyValue()
    {
        var stream = new MemoryStream();
        var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.TryWriteAnyValue("key", "value");
        writer.WriteEndObject();
        writer.Flush();

        var doc = JsonDocument.Parse(stream.ToArray());
        var root = doc.RootElement;
        var key = root.GetProperty("key");
        var value = key.GetProperty("stringValue");
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        Assert.Equal("value", value.GetString());
    }
}
#endif
