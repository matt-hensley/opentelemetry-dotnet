// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using OpenTelemetry.Exporter.OpenTelemetryProtocol;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter;

internal class OtlpStdoutLogExporter : BaseExporter<LogRecord>
{
    private readonly SdkLimitOptions sdkLimitOptions;
    private readonly ExperimentalOptions experimentalOptions;
    private Resource? resource;
    private Stream? output;

    internal OtlpStdoutLogExporter()
        : this(null, new(), new())
    {
    }

    internal OtlpStdoutLogExporter(Stream? output, SdkLimitOptions sdkLimitOptions, ExperimentalOptions experimentalOptions)
    {
        Debug.Assert(sdkLimitOptions != null, "sdkLimitOptions was null");
        Debug.Assert(experimentalOptions != null, "experimentalOptions was null");

        this.output = output ?? Console.OpenStandardOutput();
        this.sdkLimitOptions = sdkLimitOptions!;
        this.experimentalOptions = experimentalOptions!;
    }

    internal Resource Resource => this.resource ??= this.ParentProvider.GetResource();

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        using var scope = SuppressInstrumentationScope.Begin();
        using var writer = new Utf8JsonWriter(this.output!);
        writer.WriteStartObject();
        writer.WriteStartArray("resourceLogs");

        writer.WriteStartObject();
        this.WriteResource(writer);
        this.WriteScopeLogs(batch, writer);
        writer.WriteEndObject();

        writer.WriteEndArray(); // resourceLogs[]
        writer.WriteEndObject();
        writer.Flush();

        return ExportResult.Success;
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        return true;
    }

    private void WriteResource(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("resource");
        writer.WriteStartArray("attributes");

        foreach (var attribute in this.Resource.Attributes)
        {
            writer.WriteStartObject();
            writer.WriteString("key", attribute.Key);
            writer.TryWriteAnyValue("value", attribute.Value);
            writer.WriteEndObject();
        }

        writer.WriteEndArray(); // attributes[]
        writer.WriteEndObject(); // resource{}
    }

    private void WriteScopeLogs(Batch<LogRecord> batch, Utf8JsonWriter writer)
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
