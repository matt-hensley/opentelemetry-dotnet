// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter;

public class OtlpStdoutMetricExporter : BaseExporter<Metric>
{
    private readonly SdkLimitOptions sdkLimitOptions;
    private readonly ExperimentalOptions experimentalOptions;
    private readonly Stream outputStream;
    private Resource? resource;

    public OtlpStdoutMetricExporter(OtlpExporterOptions options)
        : this(options, sdkLimitOptions: new(), experimentalOptions: new(), outputStream: Console.OpenStandardOutput())
    {
    }

    internal OtlpStdoutMetricExporter(
        OtlpExporterOptions exporterOptions,
        SdkLimitOptions sdkLimitOptions,
        ExperimentalOptions experimentalOptions,
        Stream outputStream)
    {
        this.sdkLimitOptions = sdkLimitOptions;
        this.experimentalOptions = experimentalOptions;
        this.outputStream = outputStream;
    }

    internal Resource Resource => this.resource ??= this.ParentProvider.GetResource();

    public override ExportResult Export(in Batch<Metric> metrics)
    {
        try
        {
            JsonOtlpMetricSerializer.WriteMetricsData(
                this.outputStream,
                this.sdkLimitOptions,
                this.experimentalOptions,
                this.Resource,
                metrics);

            return ExportResult.Success;
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(ex);
            return ExportResult.Failure;
        }
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        if (this.outputStream != Console.OpenStandardOutput())
        {
            this.outputStream.Dispose();
        }
        return true;
    }
} 