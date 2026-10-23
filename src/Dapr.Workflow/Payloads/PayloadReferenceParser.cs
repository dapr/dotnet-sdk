// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using System;
using System.Linq;
using System.Text.Json;

namespace Dapr.Workflow.Payloads;

internal static class PayloadReferenceParser
{
    internal static PayloadReference? Parse(string? wire)
    {
        if (string.IsNullOrEmpty(wire) || !wire.AsSpan().TrimStart().StartsWith("{"))
        {
            return null;
        }

        var firstProperty = wire.AsSpan().TrimStart()[1..].TrimStart();
        if (firstProperty.Length > 1 && firstProperty[0] == '"')
        {
            var end = firstProperty[1..].IndexOf('"');
            if (end >= 0)
            {
                var name = firstProperty.Slice(1, end);
                // Escaped names need JSON decoding. Preserve the existing malformed-envelope errors.
                if (name.IndexOf('\\') < 0 && !name.SequenceEqual(PayloadReference.SentinelProperty.AsSpan()) &&
                    !PayloadReference.LooksLikeEnvelope(wire))
                {
                    return null;
                }
            }
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(wire);
        }
        catch (JsonException) when (!PayloadReference.LooksLikeEnvelope(wire))
        {
            // Non-JSON serializers are supported; leave ordinary values to their serializer.
            return null;
        }

        using (document)
        {
            var root = document.RootElement;
            if (!root.TryGetProperty(PayloadReference.SentinelProperty, out var body) ||
                root.EnumerateObject().Count() != 1)
            {
                return null;
            }

            if (body.ValueKind != JsonValueKind.Object ||
                !body.TryGetProperty("v", out var version) ||
                version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var v))
            {
                throw new FormatException("Invalid workflow payload reference envelope.");
            }

            if (v != PayloadReference.CurrentVersion)
            {
                throw new NotSupportedException($"Workflow payload reference version {v} is not supported.");
            }

            var envelope = root.Deserialize(PayloadReferenceJsonContext.Default.PayloadEnvelope);
            var value = envelope?.Ref;
            if (value is null || string.IsNullOrWhiteSpace(value.Store) ||
                string.IsNullOrWhiteSpace(value.Key) || string.IsNullOrWhiteSpace(value.Sha256) ||
                !body.TryGetProperty("size", out var size) || !size.TryGetInt64(out var length) || length < 0 ||
                value.Stages?.Any(stage => stage is null || string.IsNullOrWhiteSpace(stage.N)) == true)
            {
                throw new FormatException("Invalid workflow payload reference envelope.");
            }

            return new PayloadReference(value.Store, value.Key, value.Size, value.Sha256,
                value.Stages?.Select(stage => new PayloadStage(stage.N, stage.M)).ToArray(), value.V);
        }
    }
}
