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

using Dapr.Workflow.Payloads;

namespace Dapr.Workflow.BinaryStore.Extensions;


/// <summary>
/// Extension methods that let orchestrator code resolve offloaded payloads from a
/// <see cref="WorkflowContext"/>.
/// </summary>
/// <remarks>
/// These methods are read-only by design. Writing payloads from an orchestrator would execute on
/// every replay; produce references in activities or from the client instead.
/// </remarks>
public static class WorkflowContextBinaryStoreExtensions
{
    /// <summary>
    /// Retrieves and deserializes the payload identified by <paramref name="reference"/>.
    /// Results are cached for the remainder of the current orchestrator execution.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the payload to.</typeparam>
    /// <param name="context">The workflow context.</param>
    /// <param name="reference">The reference to resolve.</param>
    /// <returns>The deserialized payload.</returns>
    /// <exception cref="InvalidOperationException">Binary store payload support is not registered.</exception>
    /// <exception cref="PayloadNotFoundException">The stored payload no longer exists.</exception>
    public static Task<T> ResolvePayloadAsync<T>(this WorkflowContext context, PayloadReference<T> reference)
        => GetResolver(context).ResolveAsync<T>(reference);
 
    /// <summary>
    /// Retrieves and deserializes the payload identified by an untyped <paramref name="reference"/>.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the payload to.</typeparam>
    /// <param name="context">The workflow context.</param>
    /// <param name="reference">The reference to resolve.</param>
    /// <returns>The deserialized payload.</returns>
    /// <exception cref="InvalidOperationException">Binary store payload support is not registered.</exception>
    public static Task<T> ResolvePayloadAsync<T>(this WorkflowContext context, PayloadReference reference)
        => GetResolver(context).ResolveAsync<T>(reference);
 
    /// <summary>
    /// Opens a read-only stream over the raw bytes identified by <paramref name="reference"/>.
    /// The caller owns the returned stream. Prefer this over <see cref="ResolvePayloadAsync{T}(WorkflowContext, PayloadReference{T})"/>
    /// for payloads too large to buffer.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <param name="reference">The reference to open.</param>
    /// <returns>A stream positioned at the start of the payload.</returns>
    /// <exception cref="InvalidOperationException">Binary store payload support is not registered.</exception>
    public static Task<Stream> OpenPayloadAsync(this WorkflowContext context, PayloadReference reference)
        => GetResolver(context).OpenAsync(reference);
 
    /// <summary>
    /// Returns a value indicating whether binary store payload support is available on this context.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <returns><see langword="true"/> if <see cref="ResolvePayloadAsync{T}(WorkflowContext, PayloadReference{T})"/> can be called.</returns>
    public static bool HasPayloadResolver(this WorkflowContext context)
        => context.GetFeature<IWorkflowPayloadResolver>() is not null;
 
    private static IWorkflowPayloadResolver GetResolver(WorkflowContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.GetFeature<IWorkflowPayloadResolver>()
               ?? throw new InvalidOperationException(
                   "Binary store payload support is not registered. Call services.AddDaprWorkflow(...).WithBinaryStorePayloads(...) at startup.");
    }
}
