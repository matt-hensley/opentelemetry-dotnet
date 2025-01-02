using System.Diagnostics;
using System.Text.Json;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter;

internal class OtlpStdoutLogExporter : BaseExporter<LogRecord>
{
    [ThreadStatic]
    private static SerializationState? threadSerializationState;

    private readonly SdkLimitOptions sdkLimitOptions;
    private readonly ExperimentalOptions experimentalOptions;
    private Resource? resource;
    private Stream? output;

    internal Resource Resource => this.resource ??= this.ParentProvider.GetResource();

    internal OtlpStdoutLogExporter(Stream? output, SdkLimitOptions sdkLimitOptions, ExperimentalOptions experimentalOptions)
    {
        Debug.Assert(sdkLimitOptions != null, "sdkLimitOptions was null");
        Debug.Assert(experimentalOptions != null, "experimentalOptions was null");

        this.output = output ?? Console.OpenStandardOutput();
        this.sdkLimitOptions = sdkLimitOptions;
        this.experimentalOptions = experimentalOptions;
    }


    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        var writer = new Utf8JsonWriter(this.output!);
        writer.WriteStartObject();
        writer.WriteStartArray("resourceLogs");

        writer.WriteStartObject();
        this.WriteResource(writer);
        this.WriteScopeLogs(batch, writer);
        writer.WriteEndObject();

        writer.WriteEndArray(); // resourceLogs[]
        writer.WriteEndObject();
        return ExportResult.Success;
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

        writer.WriteStartObject("scopes");
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

        bool bodyPopulatedFromFormattedMessage = false;
        bool isLogRecordBodySet = false;

        if (log.FormattedMessage != null)
        {
            writer.TryWriteAnyValue("body", log.FormattedMessage);
            bodyPopulatedFromFormattedMessage = true;
            isLogRecordBodySet = true;
        }

        if (log.Attributes != null)
        {
            writer.WriteStartArray("attributes");

            foreach (var attribute in log.Attributes)
            {
                // Special casing {OriginalFormat}
                // See https://github.com/open-telemetry/opentelemetry-dotnet/pull/3182
                // for explanation.
                if (attribute.Key.Equals("{OriginalFormat}") && !bodyPopulatedFromFormattedMessage)
                {
                    writer.TryWriteAnyValue("body", attribute.Value as string);
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
                writer.TryWriteAnyValue("body", log.Body);
            }
        }

        if (log.TraceId != default && log.SpanId != default)
        {
            writer.WriteString("traceId", log.TraceId.ToHexString());
            writer.WriteString("spanId", log.SpanId.ToHexString());

            //LogRecord_Flags, (uint)logRecord.TraceFlags);
        }

        writer.WriteEndObject();
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        return true;
    }

    private sealed class SerializationState
    {
        public int? AttributeValueLengthLimit;
        public int AttributeCountLimit;
    }
}

internal static class OtlpStdoutJsonTagWriterExtensions
{
    public static void TryWriteAnyValue(this Utf8JsonWriter writer, string key, object? value, int? tagValueMaxLength = null)
    {
        if (value == null)
        {
            return;
        }

        writer.WriteStartObject(key);
        OtlpStdoutJsonTagWriter.Instance.TryWriteTag(
            ref writer,
            string.Empty,
            value,
            tagValueMaxLength);
        writer.WriteEndObject();
    }
}

internal sealed class OtlpStdoutJsonTagWriter : JsonStringArrayTagWriter<Utf8JsonWriter>
{
    public static OtlpStdoutJsonTagWriter Instance { get; } = new();

    protected override void OnUnsupportedTagDropped(string tagKey, string tagValueTypeFullName) => throw new NotImplementedException();

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
