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
using System.Threading;
using System.Threading.Tasks;
using Dapr.BinaryStore;
using Grpc.Core;

namespace Dapr.Workflow.Payloads;

/// <summary>
/// Deletes the payloads recorded for a workflow instance. Resolve this service from dependency
/// injection after registering payload storage, and invoke it only when the references are no
/// longer needed by any retained history, result, or other workflow.
/// </summary>
public sealed class WorkflowPayloadCleaner
{
    private readonly DaprBinaryStoreClient _store;
    private readonly IWorkflowPayloadManifest _manifest;

    internal WorkflowPayloadCleaner(DaprBinaryStoreClient store, IWorkflowPayloadManifest manifest)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
    }

    /// <summary>
    /// Deletes every payload recorded for <paramref name="instanceId"/> and clears the manifest.
    /// Missing payloads are ignored. Other store failures propagate, leaving the manifest intact
    /// so the operation can be retried.
    /// </summary>
    /// <param name="instanceId">The purged workflow instance.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The number of payloads deleted.</returns>
    public async Task<int> PurgeAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        var deleted = 0;

        foreach (var reference in await _manifest.ListAsync(instanceId, cancellationToken).ConfigureAwait(false))
        {
            try
            {
                await _store.DeleteBinaryFileAsync(reference.StoreName, reference.Key, cancellationToken)
                    .ConfigureAwait(false);
                deleted++;
            }
            catch (DaprException ex) when (ex.HasStatusCode(StatusCode.NotFound))
            {
                // Already gone; nothing to do.
            }
        }

        await _manifest.ClearAsync(instanceId, cancellationToken).ConfigureAwait(false);
        return deleted;
    }
}
