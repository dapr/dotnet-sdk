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
using System.Threading;
using System.Threading.Tasks;
using Dapr.Workflow.Serialization;

namespace Dapr.Workflow.Payloads;

/// <summary>
/// The default <see cref="IWorkflowPayloadConverter"/>. Delegates directly to
/// <see cref="IWorkflowSerializer"/> and performs no transformation. If a
/// <see cref="PayloadReference"/> envelope is encountered during deserialization into a
/// non-reference type, a <see cref="PayloadNotResolvableException"/> is thrown so that the absence
/// of an offload-capable converter is explicit rather than a confusing deserialization failure.
/// </summary>
internal sealed class DefaultWorkflowPayloadConverter : IWorkflowPayloadConverter
{
    private readonly IWorkflowSerializer _serializer;
 
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultWorkflowPayloadConverter"/> class.
    /// </summary>
    /// <param name="serializer">The workflow serializer used for all payloads.</param>
    public DefaultWorkflowPayloadConverter(IWorkflowSerializer serializer)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }
 
    /// <inheritdoc />
    public ValueTask<string> ToWireAsync<T>(T value, PayloadSite site, CancellationToken cancellationToken = default)
        => new(_serializer.Serialize(value));
 
    /// <inheritdoc />
    [RequiresUnreferencedCode("Serialization with a runtime Type may require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("Serialization with a runtime Type requires dynamic code generation.")]
    public ValueTask<string> ToWireAsync(object? value, Type? inputType, PayloadSite site, CancellationToken cancellationToken = default)
        => new(_serializer.Serialize(value, inputType));
 
    /// <inheritdoc />
    public ValueTask<T?> FromWireAsync<T>(string? wire, PayloadSite site, CancellationToken cancellationToken = default)
    {
        ThrowIfUnresolvableReference(wire, typeof(T), site);
        return new(_serializer.Deserialize<T>(wire));
    }
 
    /// <inheritdoc />
    [RequiresUnreferencedCode("Deserialization with a runtime Type may require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("Deserialization with a runtime Type requires dynamic code generation.")]
    public ValueTask<object?> FromWireAsync(string? wire, Type returnType, PayloadSite site, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(returnType);
        ThrowIfUnresolvableReference(wire, returnType, site);
        return new(_serializer.Deserialize(wire, returnType));
    }
 
    private static void ThrowIfUnresolvableReference(string? wire, Type targetType, PayloadSite site)
    {
        if (!string.IsNullOrEmpty(wire)
            && !PayloadReference.IsReferenceType(targetType)
            && PayloadReference.LooksLikeEnvelope(wire))
        {
            throw new PayloadNotResolvableException(site);
        }
    }
}
