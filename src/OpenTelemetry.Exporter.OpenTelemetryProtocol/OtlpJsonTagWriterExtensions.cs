// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol;

internal static class OtlpJsonTagWriterExtensions
{
    public static void TryWriteAnyValue(this Utf8JsonWriter writer, string key, object? value, int? tagValueMaxLength = null)
    {
        if (value == null)
        {
            return;
        }

        writer.WriteStartObject(key);
        OtlpJsonTagWriter.Instance.TryWriteTag(
            ref writer,
            string.Empty,
            value,
            tagValueMaxLength);
        writer.WriteEndObject();
    }
}
