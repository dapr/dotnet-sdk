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

using Dapr.Actors.Next.Abstractions.State;

namespace Dapr.Actors.Next.Abstractions;

/// <summary>
/// Base class for actor implementations.
/// </summary>
public abstract class Actor : IActor
{
    /// <summary>
    /// Gets the current actor id.
    /// </summary>
    protected abstract ActorId Id { get; }

    /// <summary>
    /// Gets the actor state accessor for the current activation.
    /// </summary>
    protected abstract IActorStateAccessor State { get; }

    /// <summary>
    /// Runs when the actor activation starts.
    /// </summary>
    protected virtual ValueTask OnActivateAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    /// <summary>
    /// Runs when the actor activation ends.
    /// </summary>
    protected virtual ValueTask OnDeactivateAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    /// <summary>
    /// Runs before an actor method turn is invoked.
    /// </summary>
    protected virtual ValueTask OnPreActorMethodAsync(ActorMethodContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    /// <summary>
    /// Runs after an actor method turn is invoked.
    /// </summary>
    protected virtual ValueTask OnPostActorMethodAsync(ActorMethodContext context, Exception? exception, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    /// <summary>
    /// Invokes the protected activation lifecycle hook for generated runtime delegates.
    /// </summary>
    public ValueTask InvokeOnActivateAsync(CancellationToken cancellationToken = default) => OnActivateAsync(cancellationToken);

    /// <summary>
    /// Invokes the protected deactivation lifecycle hook for generated runtime delegates.
    /// </summary>
    public ValueTask InvokeOnDeactivateAsync(CancellationToken cancellationToken = default) => OnDeactivateAsync(cancellationToken);

    /// <summary>
    /// Invokes the protected pre-method lifecycle hook for generated runtime delegates.
    /// </summary>
    public ValueTask InvokeOnPreActorMethodAsync(ActorMethodContext context, CancellationToken cancellationToken = default) =>
        OnPreActorMethodAsync(context, cancellationToken);

    /// <summary>
    /// Invokes the protected post-method lifecycle hook for generated runtime delegates.
    /// </summary>
    public ValueTask InvokeOnPostActorMethodAsync(ActorMethodContext context, Exception? exception, CancellationToken cancellationToken = default) =>
        OnPostActorMethodAsync(context, exception, cancellationToken);
}
