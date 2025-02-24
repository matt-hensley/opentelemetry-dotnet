// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry;

public static class OtlpStdoutExporterHelperExtensions
{
    public static TracerProviderBuilder AddOtlpStdoutExporter(
        this TracerProviderBuilder builder,
        Action<OtlpExporterOptions>? configure = null)
    {
        Guard.ThrowIfNull(builder);

        var options = new OtlpExporterOptions
        {
            Protocol = OtlpExportProtocol.ExperimentalStdout
        };

        configure?.Invoke(options);

        return builder.AddProcessor(new SimpleActivityExportProcessor(new OtlpStdoutTraceExporter(options)));
    }

    public static MeterProviderBuilder AddOtlpStdoutExporter(
        this MeterProviderBuilder builder,
        Action<OtlpExporterOptions>? configure = null)
    {
        Guard.ThrowIfNull(builder);

        var options = new OtlpExporterOptions
        {
            Protocol = OtlpExportProtocol.ExperimentalStdout
        };

        configure?.Invoke(options);

        return builder.AddReader(new BaseExportingMetricReader(new OtlpStdoutMetricExporter(options)));
    }

    public static OpenTelemetryLoggerOptions AddOtlpStdoutExporter(
        this OpenTelemetryLoggerOptions options,
        Action<OtlpExporterOptions>? configure = null)
    {
        Guard.ThrowIfNull(options);

        var exporterOptions = new OtlpExporterOptions
        {
            Protocol = OtlpExportProtocol.ExperimentalStdout
        };

        configure?.Invoke(exporterOptions);

        return options.AddProcessor(new SimpleLogRecordExportProcessor(new OtlpStdoutLogExporter(exporterOptions)));
    }
} 