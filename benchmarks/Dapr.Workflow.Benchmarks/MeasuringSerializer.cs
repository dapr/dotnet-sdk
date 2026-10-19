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

using System.Text.Json;
using System.Text.Json.Serialization;
using Dapr.Common.Serialization;

namespace Dapr.Workflow.Benchmarks;

internal static class MeasuringSerializer
{
    internal static JsonDaprSerializer Create(MeasurementWindow window)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { IncludeFields = true };
        options.Converters.Add(new DocumentConverter(window));
        return new JsonDaprSerializer(options);
    }

    private sealed class DocumentConverter(MeasurementWindow window) : JsonConverter<BenchmarkDocument>
    {
        private readonly JsonSerializerOptions inner = new(JsonSerializerDefaults.Web) { IncludeFields = true };

        public override BenchmarkDocument Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var start = reader.TokenStartIndex;
            var document = JsonSerializer.Deserialize<BenchmarkDocument>(ref reader, inner)
                ?? throw new JsonException("Expected a benchmark document.");
            window.Active?.DocumentRead(reader.BytesConsumed - start);
            return document;
        }

        public override void Write(Utf8JsonWriter writer, BenchmarkDocument value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value, inner);
    }
}
