// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

internal static class JsonOtlpTraceSerializer
{
    public static void WriteTracesData(
        Stream outputStream,
        SdkLimitOptions sdkLimitOptions,
        ExperimentalOptions experimentalOptions,
        Resource resource,
        in Batch<Activity> traces)
    {
        using var writer = new Utf8JsonWriter(outputStream, new JsonWriterOptions { Indented = false });

        writer.WriteStartObject();
        writer.WritePropertyName("resourceSpans");
        writer.WriteStartArray();
        writer.WriteStartObject();

        WriteResource(writer, resource);

        writer.WritePropertyName("scopeSpans");
        writer.WriteStartArray();
        writer.WriteStartObject();

        writer.WritePropertyName("scope");
        writer.WriteStartObject();
        writer.WriteString("name", "OpenTelemetry.Exporter.OpenTelemetryProtocol");
        writer.WriteEndObject();

        writer.WritePropertyName("spans");
        writer.WriteStartArray();

        foreach (var activity in traces)
        {
            WriteSpan(writer, activity);
        }

        writer.WriteEndArray(); // spans
        writer.WriteEndObject(); // scope
        writer.WriteEndArray(); // scopeSpans
        writer.WriteEndObject(); // resourceSpans
        writer.WriteEndArray(); // root array
        writer.WriteEndObject(); // root object

        writer.Flush();
        outputStream.WriteByte((byte)'\n');
    }

    private static void WriteResource(Utf8JsonWriter writer, Resource resource)
    {
        writer.WritePropertyName("attributes");
        writer.WriteStartArray();
        foreach (var attribute in resource.Attributes)
        {
            writer.WriteStartObject();
            writer.WriteString("key", attribute.Key);
            WriteAnyValue(writer, "value", attribute.Value);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteSpan(Utf8JsonWriter writer, Activity activity)
    {
        writer.WriteStartObject();

        writer.WriteString("traceId", activity.TraceId.ToHexString());
        writer.WriteString("spanId", activity.SpanId.ToHexString());
        if (activity.ParentSpanId != default)
        {
            writer.WriteString("parentSpanId", activity.ParentSpanId.ToHexString());
        }

        writer.WriteString("name", activity.DisplayName);
        writer.WriteString("kind", ActivityKindToString(activity.Kind));
        writer.WriteNumber("startTimeUnixNano", activity.StartTimeUtc.ToUnixTimeNanoseconds());
        writer.WriteNumber("endTimeUnixNano", activity.StartTimeUtc.Add(activity.Duration).ToUnixTimeNanoseconds());

        if (activity.Status != ActivityStatusCode.Unset)
        {
            writer.WritePropertyName("status");
            writer.WriteStartObject();
            writer.WriteString("code", activity.Status.ToString().ToUpperInvariant());
            if (!string.IsNullOrEmpty(activity.StatusDescription))
            {
                writer.WriteString("message", activity.StatusDescription);
            }
            writer.WriteEndObject();
        }

        if (activity.TagObjects.Any())
        {
            writer.WritePropertyName("attributes");
            writer.WriteStartArray();
            foreach (var tag in activity.TagObjects)
            {
                writer.WriteStartObject();
                writer.WriteString("key", tag.Key);
                WriteAnyValue(writer, "value", tag.Value);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        if (activity.Events.Any())
        {
            writer.WritePropertyName("events");
            writer.WriteStartArray();
            foreach (var evt in activity.Events)
            {
                writer.WriteStartObject();
                writer.WriteString("name", evt.Name);
                writer.WriteNumber("timeUnixNano", evt.Timestamp.ToUnixTimeNanoseconds());

                if (evt.Tags.Any())
                {
                    writer.WritePropertyName("attributes");
                    writer.WriteStartArray();
                    foreach (var tag in evt.Tags)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("key", tag.Key);
                        WriteAnyValue(writer, "value", tag.Value);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        if (activity.Links.Any())
        {
            writer.WritePropertyName("links");
            writer.WriteStartArray();
            foreach (var link in activity.Links)
            {
                writer.WriteStartObject();
                writer.WriteString("traceId", link.Context.TraceId.ToHexString());
                writer.WriteString("spanId", link.Context.SpanId.ToHexString());

                if (link.Tags.Any())
                {
                    writer.WritePropertyName("attributes");
                    writer.WriteStartArray();
                    foreach (var tag in link.Tags)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("key", tag.Key);
                        WriteAnyValue(writer, "value", tag.Value);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static string ActivityKindToString(ActivityKind kind) => kind switch
    {
        ActivityKind.Server => "SPAN_KIND_SERVER",
        ActivityKind.Client => "SPAN_KIND_CLIENT",
        ActivityKind.Producer => "SPAN_KIND_PRODUCER",
        ActivityKind.Consumer => "SPAN_KIND_CONSUMER",
        ActivityKind.Internal => "SPAN_KIND_INTERNAL",
        _ => "SPAN_KIND_UNSPECIFIED"
    };

    private static void WriteAnyValue(Utf8JsonWriter writer, string propertyName, object value)
    {
        writer.WritePropertyName(propertyName);
        switch (value)
        {
            case string s:
                writer.WriteStringValue(s);
                break;
            case int i:
                writer.WriteNumberValue(i);
                break;
            case long l:
                writer.WriteNumberValue(l);
                break;
            case double d:
                writer.WriteNumberValue(d);
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case Array arr:
                writer.WriteStartArray();
                foreach (var item in arr)
                {
                    WriteAnyValue(writer, string.Empty, item);
                }
                writer.WriteEndArray();
                break;
            default:
                writer.WriteStringValue(value?.ToString() ?? string.Empty);
                break;
        }
    }
} 