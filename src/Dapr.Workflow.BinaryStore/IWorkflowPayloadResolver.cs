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

namespace Dapr.Workflow.BinaryStore;

/// <summary>
/// Resolves and creates <see cref="PayloadReference"/> instances against a binary store. Exposed to
/// orchestrator code as a <see cref="WorkflowContext"/> feature and to activity code via dependency
/// injection.
/// </summary>
/// <remarks>
/// Orchestrator code must treat this interface as read-only. Resolution of an existing reference is
/// replay-safe because stored payloads are immutable and content-addressed; creating new payloads
/// from an orchestrator is not, and the <see cref="WorkflowContext"/> extension methods deliberately
/// do not expose <see cref="StoreAsync{T}"/>.
/// </remarks>
public interface IWorkflowPayloadResolver
{
    /// <summary>
    /// Retrieves and deserializes the payload identified by <paramref name="reference"/>.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the payload to.</typeparam>
    /// <param name="reference">The reference to resolve.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The deserialized payload.</returns>
    /// <exception cref="PayloadNotFoundException">The stored payload no longer exists.</exception>
    /// <exception cref="PayloadIntegrityException">The retrieved bytes did not match the recorded hash.</exception>
    Task<T> ResolveAsync<T>(PayloadReference reference, CancellationToken cancellationToken = default);
 
    /// <summary>
    /// Opens a read-only stream over the raw stored bytes identified by <paramref name="reference"/>
    /// without buffering or deserializing them. The caller owns the returned stream.
    /// </summary>
    /// <param name="reference">The reference to open.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A stream positioned at the start of the payload.</returns>
    /// <exception cref="PayloadNotFoundException">The stored payload no longer exists.</exception>
    Task<Stream> OpenAsync(PayloadReference reference, CancellationToken cancellationToken = default);
 
    /// <summary>
    /// Serializes and stores <paramref name="value"/> under a key scoped to <paramref name="instanceId"/>
    /// and returns a reference to it. Intended for activities and clients that want to produce a
    /// reference explicitly rather than rely on automatic offloading.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to store.</param>
    /// <param name="instanceId">The workflow instance that owns the payload.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A typed reference to the stored payload.</returns>
    Task<PayloadReference<T>> StoreAsync<T>(T value, string instanceId, CancellationToken cancellationToken = default);
}
