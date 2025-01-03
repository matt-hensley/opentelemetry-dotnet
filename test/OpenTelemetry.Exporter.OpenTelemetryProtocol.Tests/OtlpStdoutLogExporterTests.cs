// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET8_0_OR_GREATER

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using Xunit;
using OtlpLogs = OpenTelemetry.Proto.Logs.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpStdoutLogExporterTests
{
    [Fact]
    public void OtlpStdoutJsonTagWriter_TryWriteTag()
    {
        var stream = new MemoryStream();
        var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        var success = OtlpJsonTagWriter.Instance.TryWriteTag(ref writer, "key", "value");
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

    [Fact]
    public void OtlpStdoutLogExporter_Export()
    {
        var logRecords = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.AddInMemoryExporter(logRecords);
            });
        });
        var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
        logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
        using var stream = new MemoryStream();
        using (var exporter = new OtlpStdoutLogExporter(
            stream,
            new OpenTelemetryProtocol.Implementation.SdkLimitOptions(),
            new OpenTelemetryProtocol.Implementation.ExperimentalOptions()))
        {
            exporter.Export(new Batch<LogRecord>(logRecords.ToArray(), logRecords.Count));
        }

        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);
        var str = Encoding.UTF8.GetString(stream.ToArray());
        var logsData = OtlpLogs.LogsData.Parser.ParseJson(str);
        Assert.NotNull(logsData);

        var resourceLogs = Assert.Single(logsData.ResourceLogs);
        var scopeLog = Assert.Single(resourceLogs.ScopeLogs);
        var logRecord = Assert.Single(scopeLog.LogRecords);
        Assert.Equal("Information", logRecord.SeverityText);
        Assert.Equal(OtlpLogs.SeverityNumber.Info, logRecord.SeverityNumber);
    }
}
#endif
