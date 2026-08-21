// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using BenchmarkDotNet.Attributes;

namespace Benchmarks.Exporter;

/// <summary>
/// Short-run variant used to validate the comparison harness quickly.
/// </summary>
[ShortRunJob]
public class ProtobufOtlpArrayTagSerializationShortRunBenchmarks : ProtobufOtlpArrayTagSerializationBenchmarks
{
}
#endif
