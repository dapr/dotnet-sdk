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
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Dapr.Common.Serialization;

namespace Dapr.Workflow.Payloads;

internal static class WorkflowPayloadJsonSerializer
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static T? Deserialize<T>(JsonDaprSerializer serializer, ReadOnlySpan<byte> utf8Json) =>
        HasInvalidUtf8(utf8Json)
            ? serializer.Deserialize<T>(Encoding.UTF8.GetString(utf8Json))
            : serializer.DeserializeFromUtf8Bytes<T>(utf8Json);

    [RequiresUnreferencedCode("JSON deserialization with a runtime Type may require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("JSON deserialization with a runtime Type requires dynamic code generation.")]
    internal static object? Deserialize(JsonDaprSerializer serializer, ReadOnlySpan<byte> utf8Json, Type returnType) =>
        HasInvalidUtf8(utf8Json)
            ? serializer.Deserialize(Encoding.UTF8.GetString(utf8Json), returnType)
            : serializer.DeserializeFromUtf8Bytes(utf8Json, returnType);

    private static bool HasInvalidUtf8(ReadOnlySpan<byte> bytes)
    {
        // Preserve the replacement behavior of the workflow's original UTF-8-to-string path.
        try
        {
            StrictUtf8.GetCharCount(bytes);
            return false;
        }
        catch (DecoderFallbackException)
        {
            return true;
        }
    }
}
