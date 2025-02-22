// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

internal sealed class JsonOtlpLogSerializer
{
    private readonly SdkLimitOptions sdkLimitOptions;
    private readonly ExperimentalOptions experimentalOptions;

    public JsonOtlpLogSerializer(SdkLimitOptions sdkLimitOptions, ExperimentalOptions experimentalOptions)
    {
        this.sdkLimitOptions = sdkLimitOptions;
        this.experimentalOptions = experimentalOptions;
    }

    public void Serialize(Stream outputStream, in Batch<LogRecord> batch, Resource resource)
    {
        using var writer = new Utf8JsonWriter(outputStream);
        writer.WriteStartObject();
        writer.WriteStartArray("resourceLogs");

        writer.WriteStartObject();
        this.WriteResource(writer, resource);
        this.WriteScopeLogs(writer, batch);
        writer.WriteEndObject();

        writer.WriteEndArray(); // resourceLogs[]
        writer.WriteEndObject();
        writer.Flush();
    }

    private void WriteResource(Utf8JsonWriter writer, Resource resource)
    {
        writer.WriteStartObject("resource");
        writer.WriteStartArray("attributes");

        foreach (var attribute in resource.Attributes)
        {
            writer.WriteStartObject();
            writer.WriteString("key", attribute.Key);
            writer.TryWriteAnyValue("value", attribute.Value);
            writer.WriteEndObject();
        }

        writer.WriteEndArray(); // attributes[]
        writer.WriteEndObject(); // resource{}
    }

    private void WriteScopeLogs(Utf8JsonWriter writer, in Batch<LogRecord> batch)
    {
        writer.WriteStartArray("scopeLogs");
        writer.WriteStartObject();

        writer.WriteStartObject("scope");
        writer.WriteEndObject(); // scopes{}

        writer.WriteStartArray("logRecords");

        foreach (var log in batch)
        {
            this.WriteLogRecord(writer, log);
        }

        writer.WriteEndArray(); // logRecords[]
        writer.WriteEndObject();
        writer.WriteEndArray(); // scopeLogs[]
    }

    private void WriteLogRecord(Utf8JsonWriter writer, LogRecord log)
    {
        var attributeValueLengthLimit = this.sdkLimitOptions.LogRecordAttributeValueLengthLimit;
        var attributeCountLimit = this.sdkLimitOptions.LogRecordAttributeCountLimit ?? int.MaxValue;

        var timestamp = (ulong)log.Timestamp.ToUnixTimeNanoseconds();
        writer.WriteStartObject();
        writer.WriteString("timeUnixNano", timestamp.ToString());

        if (!string.IsNullOrWhiteSpace(log.SeverityText))
        {
            writer.WriteString("severityText", log.SeverityText);
        }
        else if (log.Severity.HasValue)
        {
            writer.WriteString("severityText", log.Severity.Value.ToShortName());
        }

        writer.WriteNumber("severityNumber", log.Severity.HasValue ? (int)log.Severity : 0);

        string body = string.Empty;
        bool bodyPopulatedFromFormattedMessage = false;
        bool isLogRecordBodySet = false;

        if (log.FormattedMessage != null)
        {
            body = log.FormattedMessage;
            bodyPopulatedFromFormattedMessage = true;
            isLogRecordBodySet = true;
        }

        if (log.Attributes != null && log.Attributes.Count > 0)
        {
            writer.WriteStartArray("attributes");

            foreach (var attribute in log.Attributes)
            {
                // Special casing {OriginalFormat}
                // See https://github.com/open-telemetry/opentelemetry-dotnet/pull/3182
                // for explanation.
                if (attribute.Key.Equals("{OriginalFormat}") && !bodyPopulatedFromFormattedMessage)
                {
                    body = (attribute.Value as string) ?? string.Empty;
                    isLogRecordBodySet = true;
                }
                else
                {
                    writer.WriteStartObject();
                    writer.WriteString("key", attribute.Key);
                    writer.TryWriteAnyValue("value", attribute.Value, attributeValueLengthLimit);
                    writer.WriteEndObject();
                }
            }

            writer.WriteEndArray();

            // Supports setting Body directly on LogRecord for the Logs Bridge API.
            if (!isLogRecordBodySet && log.Body != null)
            {
                // If {OriginalFormat} is not present in the attributes,
                // use logRecord.Body if it is set.
                body = log.Body;
            }
        }

        writer.TryWriteAnyValue("body", body);

        if (log.TraceId != default && log.SpanId != default)
        {
            writer.WriteString("traceId", log.TraceId.ToHexString());
            writer.WriteString("spanId", log.SpanId.ToHexString());

            // LogRecord_Flags, (uint)logRecord.TraceFlags);
        }

        writer.WriteEndObject();
    }
} 