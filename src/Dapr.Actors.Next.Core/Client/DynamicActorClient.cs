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

using Dapr.Actors.Next.Abstractions.Registry;
using Dapr.Actors.Next.Core;
using Dapr.Actors.Next.Core.Serialization;

namespace Dapr.Actors.Next.Core.Client;

/// <summary>
/// Weakly typed actor client that accepts and returns JSON strings.
/// </summary>
public sealed class DynamicActorClient(IActorInvocationClient client, IActorWireSerializer serializer) : IDynamicActorClient
{
    /// <inheritdoc />
    public async Task<string?> InvokeAsync(string actorType, string actorId, string method, string argsJson, CancellationToken cancellationToken = default)
    {
        var result = await client.InvokeAsync(actorType, actorId, method, serializer.JsonToBytes(argsJson), ActorHeaders.Empty, cancellationToken).ConfigureAwait(false);
        return result is null ? null : serializer.BytesToJson(result);
    }
}
