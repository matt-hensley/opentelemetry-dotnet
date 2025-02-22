// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter;

internal class OtlpStdoutLogExporter : BaseExporter<LogRecord>
{
    private readonly Stream output;
    private readonly JsonOtlpLogSerializer serializer;
    private Resource? resource;

    internal OtlpStdoutLogExporter()
        : this(null, new(), new())
    {
    }

    internal OtlpStdoutLogExporter(Stream? output, SdkLimitOptions sdkLimitOptions, ExperimentalOptions experimentalOptions)
    {
        Debug.Assert(sdkLimitOptions != null, "sdkLimitOptions was null");
        Debug.Assert(experimentalOptions != null, "experimentalOptions was null");

        this.output = output ?? Console.OpenStandardOutput();
        this.serializer = new JsonOtlpLogSerializer(sdkLimitOptions!, experimentalOptions!);
    }

    internal Resource Resource => this.resource ??= this.ParentProvider.GetResource();

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        using var scope = SuppressInstrumentationScope.Begin();
        this.serializer.Serialize(this.output, batch, this.Resource);
        return ExportResult.Success;
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        return true;
    }
}
