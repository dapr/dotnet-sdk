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

using System.Text.Json;
using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Dispatching;

namespace Dapr.Actors.Next.Interpreted;

/// <summary>
/// Dispatcher for the compiled interpreted state-machine actor.
/// </summary>
public sealed class InterpretedStateMachineDispatcher : IActorDispatcher
{
    /// <inheritdoc />
    public async ValueTask<ActorDispatchResponse> DispatchAsync(IActor actor, ActorDispatchRequest request, CancellationToken cancellationToken = default)
    {
        var typed = (InterpretedStateMachineActor)actor;
        if (string.Equals(request.MethodName, "Reset", StringComparison.Ordinal)
            || string.Equals(request.MethodName, nameof(InterpretedStateMachineActor.ResetAsync), StringComparison.Ordinal))
        {
            await typed.ResetAsync(cancellationToken).ConfigureAwait(false);
            return new ActorDispatchResponse(null);
        }

        if (!string.Equals(request.MethodName, "Raise", StringComparison.Ordinal)
            && !string.Equals(request.MethodName, nameof(InterpretedStateMachineActor.RaiseAsync), StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Interpreted actor operation '{request.MethodName}' is not supported.");
        }

        var evt = JsonSerializer.Deserialize<InterpretedEvent>(request.Payload.Span)
            ?? throw new InvalidOperationException("Interpreted event payload could not be deserialized.");
        var result = await typed.RaiseAsync(evt, cancellationToken).ConfigureAwait(false);
        return new ActorDispatchResponse(JsonSerializer.SerializeToUtf8Bytes(result));
    }
}
