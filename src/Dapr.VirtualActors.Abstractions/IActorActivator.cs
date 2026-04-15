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
/// Factory interface for creating actor instances via dependency injection.
/// </summary>
/// <remarks>
/// <para>
/// The default implementation uses pre-registered factory delegates (generated at
/// compile time by the source generator) to construct actor instances with full DI
/// support. No reflection is used — the implementation is fully AOT-safe.
/// </para>
/// <para>
/// Custom implementations can add behaviors like object pooling, decoration,
/// or AOP proxy wrapping.
/// </para>
/// <para>
/// Add-on projects can replace or decorate this factory to intercept actor creation.
/// For example, an agentic framework could inject AI context into every actor:
/// </para>
/// <code>
/// services.AddDaprVirtualActors(options => { ... })
///     .UseActorActivator&lt;AgenticActorActivator&gt;();
/// </code>
/// </remarks>
public interface IActorActivator
{
    /// <summary>
    /// Creates an actor instance.
    /// </summary>
    /// <param name="actorType">The type metadata of the actor being created.</param>
    /// <param name="actorId">The identity of the actor being created.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// An <see cref="ActorActivationResult"/> containing the actor instance and a
    /// disposable scope (if applicable).
    /// </returns>
    Task<ActorActivationResult> CreateAsync(
        ActorTypeInformation actorType,
        VirtualActorId actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases resources associated with a previously created actor instance.
    /// </summary>
    /// <param name="result">The activation result from <see cref="CreateAsync"/>.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReleaseAsync(ActorActivationResult result, CancellationToken cancellationToken = default);
}

/// <summary>
/// Holds the result of an actor activation, including the actor instance and
/// an optional associated scope for cleanup.
/// </summary>
/// <param name="Actor">The created actor instance.</param>
/// <param name="Scope">
/// An optional <see cref="IAsyncDisposable"/> scope (e.g., a DI service scope)
/// that should be disposed when the actor is deactivated.
/// </param>
public sealed record ActorActivationResult(object Actor, IAsyncDisposable? Scope = null);
