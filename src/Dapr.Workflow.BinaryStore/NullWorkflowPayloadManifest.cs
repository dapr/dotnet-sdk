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
/// An <see cref="IWorkflowPayloadManifest"/> that records nothing. Purge-time cleanup is a no-op
/// and payload lifecycle is left to store-side retention policy.
/// </summary>
public sealed class NullWorkflowPayloadManifest : IWorkflowPayloadManifest
{
    /// <summary>
    /// The shared instance.
    /// </summary>
    public static readonly NullWorkflowPayloadManifest Instance = new();
 
    /// <inheritdoc />
    public Task RecordAsync(string instanceId, PayloadReference reference, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
 
    /// <inheritdoc />
    public Task<IReadOnlyCollection<PayloadReference>> ListAsync(string instanceId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<PayloadReference>>([]);
 
    /// <inheritdoc />
    public Task ClearAsync(string instanceId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
