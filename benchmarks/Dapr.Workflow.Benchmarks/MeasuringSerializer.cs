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

using System.Diagnostics.CodeAnalysis;
using Dapr.Common.Serialization;

namespace Dapr.Workflow.Benchmarks;

internal sealed class MeasuringSerializer(MeasurementWindow window) : IDaprSerializer
{
    private readonly JsonDaprSerializer inner = new();
    public string Serialize<T>(T value) => inner.Serialize(value);

    [RequiresUnreferencedCode("Benchmark uses runtime JSON types.")]
    [RequiresDynamicCode("Benchmark uses runtime JSON types.")]
    public string Serialize(object? value, Type? inputType = null) => inner.Serialize(value, inputType);

    public T? Deserialize<T>(string? data)
    {
        var result = inner.Deserialize<T>(data);
        if (typeof(T) == typeof(BenchmarkDocument))
            window.Active?.DocumentRead(data);
        return result;
    }

    [RequiresUnreferencedCode("Benchmark uses runtime JSON types.")]
    [RequiresDynamicCode("Benchmark uses runtime JSON types.")]
    public object? Deserialize(string? data, Type returnType)
    {
        var result = inner.Deserialize(data, returnType);
        if (returnType == typeof(BenchmarkDocument))
            window.Active?.DocumentRead(data);
        return result;
    }
}
