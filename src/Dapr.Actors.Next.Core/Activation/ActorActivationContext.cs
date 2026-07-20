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

using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.State;

namespace Dapr.Actors.Next.Core.Activation;

/// <summary>
/// Exposes activation-scoped runtime services to hand-written factories and generated factories.
/// </summary>
public sealed class ActorActivationContext
{
    internal ActorActivationContext(ActorId actorId, IActorStateAccessor state)
    {
        ActorId = actorId;
        State = state;
    }

    /// <summary>
    /// Gets the actor id for the activation being constructed.
    /// </summary>
    public ActorId ActorId { get; }

    /// <summary>
    /// Gets the state accessor for the activation being constructed.
    /// </summary>
    public IActorStateAccessor State { get; }
}
