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
/// Converts workflow payloads between their in-memory representation and the string form that is
/// sent to and received from the Dapr sidecar. The workflow runtime and client invoke this
/// interface at every payload boundary instead of calling <see cref="IWorkflowSerializer"/>
/// directly, which allows an implementation to transform, offload, or rehydrate payloads
/// transparently.
/// </summary>
/// <remarks>
/// <para>
/// The string representation mirrors <see cref="Dapr.Common.Serialization.IDaprSerializer"/>:
/// a <see langword="null"/> value serializes to an empty string, and an empty or
/// <see langword="null"/> string deserializes to <c>default</c>.
/// </para>
/// <para>
/// Prefer the generic overloads; the <see cref="Type"/>-based overloads exist for call sites that
/// only know the type at runtime and are annotated accordingly for trimming and AOT.
/// </para>
/// <para>
/// The default implementation delegates to <see cref="IWorkflowSerializer"/> with no further
/// processing. Register a replacement with <c>IServiceCollection.Replace</c> or by calling a
/// package-provided extension such as <c>WithBinaryStorePayloads</c>.
/// </para>
/// <para>
/// Implementations must be thread-safe and must not perform I/O when invoked from inside an
/// orchestrator execution. The runtime guarantees that orchestrator-side calls only ever pass
/// values that are either small literals or already-materialized <see cref="PayloadReference"/>
/// envelopes.
/// </para>
/// </remarks>
public interface IWorkflowPayloadConverter
{
    /// <summary>
    /// Serializes a value of a known type into the string that will be persisted by the Dapr runtime.
    /// </summary>
    /// <typeparam name="T">The compile-time type of the value.</typeparam>
    /// <param name="value">The value to serialize. May be <see langword="null"/>.</param>
    /// <param name="site">The boundary at which the payload is being produced.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The serialized payload, or an empty string for <see langword="null"/>. May be a reference envelope rather than the value itself.</returns>
    ValueTask<string> ToWireAsync<T>(T value, PayloadSite site, CancellationToken cancellationToken = default);
 
    /// <summary>
    /// Serializes a value using a runtime type hint.
    /// </summary>
    /// <param name="value">The value to serialize. May be <see langword="null"/>.</param>
    /// <param name="inputType">Optional type hint. If <see langword="null"/>, the runtime type of <paramref name="value"/> is used.</param>
    /// <param name="site">The boundary at which the payload is being produced.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The serialized payload, or an empty string for <see langword="null"/>.</returns>
    [RequiresUnreferencedCode("Serialization with a runtime Type may require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("Serialization with a runtime Type requires dynamic code generation.")]
    ValueTask<string> ToWireAsync(object? value, Type? inputType, PayloadSite site, CancellationToken cancellationToken = default);
 
    /// <summary>
    /// Deserializes a string received from the Dapr runtime into a value of a known type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize into. If this is <see cref="PayloadReference"/> or a
    /// closed <see cref="PayloadReference{T}"/>, implementations must return the reference without resolving it.</typeparam>
    /// <param name="wire">The serialized payload. May be <see langword="null"/> or empty.</param>
    /// <param name="site">The boundary at which the payload is being consumed.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The deserialized value, or <c>default(T)</c> if <paramref name="wire"/> is <see langword="null"/> or empty.</returns>
    ValueTask<T?> FromWireAsync<T>(string? wire, PayloadSite site, CancellationToken cancellationToken = default);
 
    /// <summary>
    /// Deserializes a string received from the Dapr runtime into a value of a runtime type.
    /// </summary>
    /// <param name="wire">The serialized payload. May be <see langword="null"/> or empty.</param>
    /// <param name="returnType">The type to deserialize into. Reference types are returned without resolution.</param>
    /// <param name="site">The boundary at which the payload is being consumed.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The deserialized value, or <see langword="null"/> if <paramref name="wire"/> is <see langword="null"/> or empty.</returns>
    [RequiresUnreferencedCode("Deserialization with a runtime Type may require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("Deserialization with a runtime Type requires dynamic code generation.")]
    ValueTask<object?> FromWireAsync(string? wire, Type returnType, PayloadSite site, CancellationToken cancellationToken = default);
}
