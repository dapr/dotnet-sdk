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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Common.Serialization;

namespace Dapr.Workflow.Payloads;

internal sealed class BinaryStoreWorkflowPayloadResolver(
    BinaryStorePayloadConverter codec,
    IDaprSerializer serializer) : IWorkflowPayloadResolver
{
    public async Task<T> ResolveAsync<T>(PayloadReference reference, CancellationToken cancellationToken = default)
    {
        var serialized = await codec.ResolveStringAsync(reference, cancellationToken).ConfigureAwait(false);
        return serializer.Deserialize<T>(serialized)
               ?? throw new InvalidOperationException(
                   $"Payload '{reference.Key}' deserialized to null for type {typeof(T)}.");
    }

    public Task<Stream> OpenAsync(PayloadReference reference, CancellationToken cancellationToken = default)
        => codec.OpenAsync(reference, cancellationToken);

    public async Task<PayloadReference<T>> StoreAsync<T>(T value, string instanceId,
        CancellationToken cancellationToken = default)
    {
        var reference = await codec.StoreSerializedAsync(serializer.Serialize(value), instanceId, cancellationToken)
            .ConfigureAwait(false);
        return reference.AsTyped<T>();
    }
}
