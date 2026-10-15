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
/// Tracks the payload references written on behalf of each workflow instance so they can be
/// deleted when the instance is purged. The binary store API has no list or prefix-delete
/// operation, so without a manifest the SDK cannot discover an instance's payloads after the fact.
/// </summary>
/// <remarks>
/// The default registration is <see cref="NullWorkflowPayloadManifest"/>, which records nothing and
/// makes purge-time cleanup a no-op. Applications that need cleanup should register an
/// implementation backed by a durable store. When the runtime gains a prefix-delete RPC this
/// abstraction can be bypassed entirely.
/// </remarks>
public interface IWorkflowPayloadManifest
{
    /// <summary>
    /// Records that <paramref name="reference"/> was written for <paramref name="instanceId"/>.
    /// Must be idempotent; the same reference may be recorded more than once.
    /// </summary>
    /// <param name="instanceId">The owning workflow instance.</param>
    /// <param name="reference">The reference that was written.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task RecordAsync(string instanceId, PayloadReference reference, CancellationToken cancellationToken = default);
 
    /// <summary>
    /// Returns every reference recorded for <paramref name="instanceId"/>.
    /// </summary>
    /// <param name="instanceId">The owning workflow instance.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The recorded references, or an empty collection if none were recorded.</returns>
    Task<IReadOnlyCollection<PayloadReference>> ListAsync(string instanceId, CancellationToken cancellationToken = default);
 
    /// <summary>
    /// Removes all records for <paramref name="instanceId"/>. Called after the referenced payloads
    /// have been deleted.
    /// </summary>
    /// <param name="instanceId">The owning workflow instance.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task ClearAsync(string instanceId, CancellationToken cancellationToken = default);
}
