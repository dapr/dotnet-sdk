// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

namespace Dapr.VirtualActors;

/// <summary>
/// Observes actor lifecycle events (activation, deactivation, method invocations).
/// </summary>
/// <remarks>
/// <para>
/// Lifecycle observers are notified of actor lifecycle transitions but cannot modify
/// the actor's behavior. They are intended for cross-cutting concerns like auditing,
/// metrics collection, and integration with external systems.
/// </para>
/// <para>
/// Unlike <see cref="IActorMiddleware"/>, observers do not participate in the method
/// invocation pipeline and cannot short-circuit or modify requests/responses. Multiple
/// observers can be registered and will be invoked in registration order.
/// </para>
/// <para>
/// Example: An agentic framework extension could observe activations to pre-load
/// vector embeddings for a conversational actor:
/// </para>
/// <code>
/// services.AddDaprVirtualActors(options => { ... })
///     .AddLifecycleObserver&lt;EmbeddingPreloadObserver&gt;();
/// </code>
/// </remarks>
public interface IActorLifecycleObserver
{
    /// <summary>
    /// Called after an actor instance has been activated.
    /// </summary>
    /// <param name="actorType">The actor type name.</param>
    /// <param name="actorId">The actor ID.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task OnActivatedAsync(string actorType, VirtualActorId actorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after an actor instance has been deactivated.
    /// </summary>
    /// <param name="actorType">The actor type name.</param>
    /// <param name="actorId">The actor ID.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task OnDeactivatedAsync(string actorType, VirtualActorId actorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after an actor method has been successfully invoked.
    /// </summary>
    /// <param name="context">The invocation context.</param>
    /// <param name="elapsed">The elapsed time of the method invocation.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task OnMethodCompletedAsync(ActorInvocationContext context, TimeSpan elapsed, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when an actor method invocation has failed.
    /// </summary>
    /// <param name="context">The invocation context.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task OnMethodFailedAsync(ActorInvocationContext context, Exception exception, CancellationToken cancellationToken = default);
}
