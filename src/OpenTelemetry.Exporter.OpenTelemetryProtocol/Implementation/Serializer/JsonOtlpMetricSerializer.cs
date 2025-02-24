// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

internal static class JsonOtlpMetricSerializer
{
    public static void WriteMetricsData(
        Stream outputStream,
        SdkLimitOptions sdkLimitOptions,
        ExperimentalOptions experimentalOptions,
        Resource resource,
        in Batch<Metric> metrics)
    {
        using var writer = new Utf8JsonWriter(outputStream, new JsonWriterOptions { Indented = false });

        writer.WriteStartObject();
        writer.WritePropertyName("resourceMetrics");
        writer.WriteStartArray();

        writer.WriteStartObject();
        WriteResource(writer, resource);

        writer.WritePropertyName("scopeMetrics");
        writer.WriteStartArray();

        var currentScope = string.Empty;
        var firstScope = true;

        foreach (var metric in metrics)
        {
            if (metric.MeterName != currentScope)
            {
                if (!firstScope)
                {
                    writer.WriteEndArray(); // metrics
                    writer.WriteEndObject(); // scope
                }

                writer.WriteStartObject();
                writer.WritePropertyName("scope");
                writer.WriteStartObject();
                writer.WriteString("name", metric.MeterName);
                if (!string.IsNullOrEmpty(metric.MeterVersion))
                {
                    writer.WriteString("version", metric.MeterVersion);
                }
                writer.WriteEndObject();

                writer.WritePropertyName("metrics");
                writer.WriteStartArray();

                currentScope = metric.MeterName;
                firstScope = false;
            }

            WriteMetric(writer, metric);
        }

        if (!firstScope)
        {
            writer.WriteEndArray(); // metrics
            writer.WriteEndObject(); // scope
        }

        writer.WriteEndArray(); // scopeMetrics
        writer.WriteEndObject(); // resourceMetrics
        writer.WriteEndArray(); // root array
        writer.WriteEndObject(); // root object

        writer.Flush();
        outputStream.WriteByte((byte)'\n'); // Add newline after each batch
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

    private static void WriteMetric(Utf8JsonWriter writer, in Metric metric)
    {
        writer.WriteStartObject();
        writer.WriteString("name", metric.Name);
        writer.WriteString("description", metric.Description);
        writer.WriteString("unit", metric.Unit);

        switch (metric.MetricType)
        {
            case MetricType.LongSum:
                WriteLongSum(writer, metric);
                break;
            case MetricType.DoubleSum:
                WriteDoubleSum(writer, metric);
                break;
            case MetricType.LongGauge:
                WriteLongGauge(writer, metric);
                break;
            case MetricType.DoubleGauge:
                WriteDoubleGauge(writer, metric);
                break;
            case MetricType.Histogram:
                WriteHistogram(writer, metric);
                break;
        }

        writer.WriteEndObject();
    }

    private static void WriteLongSum(Utf8JsonWriter writer, in Metric metric)
    {
        writer.WriteString("type", "sum");
        writer.WritePropertyName("sum");
        writer.WriteStartObject();
        writer.WriteBoolean("monotonic", metric.MetricType == MetricType.LongSum);
        writer.WriteString("aggregationTemporality", "CUMULATIVE");
        
        writer.WritePropertyName("dataPoints");
        writer.WriteStartArray();
        foreach (ref readonly var metricPoint in metric.GetMetricPoints())
        {
            WriteLongDataPoint(writer, in metricPoint);
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteDoubleSum(Utf8JsonWriter writer, in Metric metric)
    {
        writer.WriteString("type", "sum");
        writer.WritePropertyName("sum");
        writer.WriteStartObject();
        writer.WriteBoolean("monotonic", metric.MetricType == MetricType.DoubleSum);
        writer.WriteString("aggregationTemporality", "CUMULATIVE");
        
        writer.WritePropertyName("dataPoints");
        writer.WriteStartArray();
        foreach (ref readonly var metricPoint in metric.GetMetricPoints())
        {
            WriteDoubleDataPoint(writer, in metricPoint);
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteLongGauge(Utf8JsonWriter writer, in Metric metric)
    {
        writer.WriteString("type", "gauge");
        writer.WritePropertyName("gauge");
        writer.WriteStartObject();
        
        writer.WritePropertyName("dataPoints");
        writer.WriteStartArray();
        foreach (ref readonly var metricPoint in metric.GetMetricPoints())
        {
            WriteLongDataPoint(writer, in metricPoint);
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteDoubleGauge(Utf8JsonWriter writer, in Metric metric)
    {
        writer.WriteString("type", "gauge");
        writer.WritePropertyName("gauge");
        writer.WriteStartObject();
        
        writer.WritePropertyName("dataPoints");
        writer.WriteStartArray();
        foreach (ref readonly var metricPoint in metric.GetMetricPoints())
        {
            WriteDoubleDataPoint(writer, in metricPoint);
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteHistogram(Utf8JsonWriter writer, in Metric metric)
    {
        writer.WriteString("type", "histogram");
        writer.WritePropertyName("histogram");
        writer.WriteStartObject();
        writer.WriteString("aggregationTemporality", "CUMULATIVE");
        
        writer.WritePropertyName("dataPoints");
        writer.WriteStartArray();
        foreach (ref readonly var metricPoint in metric.GetMetricPoints())
        {
            WriteHistogramDataPoint(writer, in metricPoint);
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteLongDataPoint(Utf8JsonWriter writer, in MetricPoint metricPoint)
    {
        writer.WriteStartObject();
        writer.WriteNumber("startTimeUnixNano", metricPoint.StartTime.ToUnixTimeNanoseconds());
        writer.WriteNumber("timeUnixNano", metricPoint.EndTime.ToUnixTimeNanoseconds());
        writer.WriteNumber("value", metricPoint.GetGaugeLastValueLong());
        WriteAttributes(writer, metricPoint.Tags);
        writer.WriteEndObject();
    }

    private static void WriteDoubleDataPoint(Utf8JsonWriter writer, in MetricPoint metricPoint)
    {
        writer.WriteStartObject();
        writer.WriteNumber("startTimeUnixNano", metricPoint.StartTime.ToUnixTimeNanoseconds());
        writer.WriteNumber("timeUnixNano", metricPoint.EndTime.ToUnixTimeNanoseconds());
        writer.WriteNumber("value", metricPoint.GetGaugeLastValueDouble());
        WriteAttributes(writer, metricPoint.Tags);
        writer.WriteEndObject();
    }

    private static void WriteHistogramDataPoint(Utf8JsonWriter writer, in MetricPoint metricPoint)
    {
        writer.WriteStartObject();
        writer.WriteNumber("startTimeUnixNano", metricPoint.StartTime.ToUnixTimeNanoseconds());
        writer.WriteNumber("timeUnixNano", metricPoint.EndTime.ToUnixTimeNanoseconds());
        writer.WriteNumber("count", metricPoint.GetHistogramCount());
        writer.WriteNumber("sum", metricPoint.GetHistogramSum());

        var buckets = metricPoint.GetHistogramBuckets();
        writer.WritePropertyName("bucketCounts");
        writer.WriteStartArray();
        foreach (var bucket in buckets)
        {
            writer.WriteNumberValue(bucket.BucketCount);
        }
        writer.WriteEndArray();

        writer.WritePropertyName("explicitBounds");
        writer.WriteStartArray();
        foreach (var bucket in buckets)
        {
            writer.WriteNumberValue(bucket.ExplicitBound);
        }
        writer.WriteEndArray();

        WriteAttributes(writer, metricPoint.Tags);
        writer.WriteEndObject();
    }

    private static void WriteAttributes(Utf8JsonWriter writer, ReadOnlyTagCollection tags)
    {
        writer.WritePropertyName("attributes");
        writer.WriteStartArray();
        foreach (var tag in tags)
        {
            writer.WriteStartObject();
            writer.WriteString("key", tag.Key);
            WriteAnyValue(writer, "value", tag.Value);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

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
            default:
                writer.WriteStringValue(value?.ToString() ?? string.Empty);
                break;
        }
    }
} 