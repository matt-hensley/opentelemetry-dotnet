// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
extern alias OpenTelemetryProtocol;

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Benchmarks.Helper;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

namespace Benchmarks.Exporter;

/// <summary>
/// Measures serialization of a span carrying several sequential array-valued attributes.
/// </summary>
[MemoryDiagnoser]
public class ProtobufOtlpArrayTagSerializationBenchmarks
{
    private const int InitialBufferSize = 750000;
    private const int ArrayTagCount = 16;
    private const int ArrayLength = 16;

    private readonly SdkLimitOptions sdkLimitOptions = new();
    private readonly Resource resource = Resource.Empty;
    private Activity activity = null!;
    private byte[] buffer = null!;

    /// <summary>
    /// Creates one activity with multiple sequential array-valued tags before each benchmark process starts.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.activity = ActivityHelper.CreateTestActivity();

        for (var i = 0; i < ArrayTagCount; i++)
        {
            var values = new int[ArrayLength];
            for (var j = 0; j < values.Length; j++)
            {
                values[j] = (i * values.Length) + j;
            }

            this.activity.SetTag($"array-{i}", values);
        }

        this.buffer = ProtobufSerializer.RentBuffer(InitialBufferSize);
    }

    /// <summary>
    /// Serializes the activity through the OTLP trace serializer route.
    /// </summary>
    [Benchmark]
    public int SerializeSequentialArrayTags()
    {
        var batch = new Batch<Activity>(new[] { this.activity }, 1);
        return ProtobufOtlpTraceSerializer.WriteTraceData(ref this.buffer, 0, this.sdkLimitOptions, this.resource, batch);
    }

    /// <summary>
    /// Returns the serializer buffer after the benchmark process exits.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        if (this.buffer != null)
        {
            ProtobufSerializer.ReturnBuffer(this.buffer);
            this.buffer = null!;
        }
    }
}

#endif
