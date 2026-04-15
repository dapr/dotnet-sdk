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

using Microsoft.Extensions.Logging;

namespace Dapr.VirtualActors.Runtime;

/// <summary>
/// The base class for all Dapr virtual actor implementations.
/// </summary>
/// <remarks>
/// <para>
/// Actors derive from this class and use the <see cref="StateManager"/> to persist state
/// and the <see cref="Host"/> to access runtime services. The actor lifecycle is managed
/// by the Dapr runtime through activation and deactivation callbacks.
/// </para>
/// <para>
/// Actor instances are constructed via DI. The <see cref="VirtualActorHost"/> parameter
/// is always supplied by the runtime:
/// <code>
/// public class MyActor(VirtualActorHost host) : VirtualActor(host), IMyActor { }
/// </code>
/// </para>
/// </remarks>
public abstract class VirtualActor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualActor"/> class.
    /// </summary>
    /// <param name="host">The host providing runtime services for this actor instance.</param>
    protected VirtualActor(VirtualActorHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        Host = host;
        Logger = host.LoggerFactory.CreateLogger(GetType());
    }

    /// <summary>
    /// Gets the host providing runtime services for this actor instance.
    /// </summary>
    public VirtualActorHost Host { get; }

    /// <summary>
    /// Gets the unique identity of this actor.
    /// </summary>
    public VirtualActorId Id => Host.Id;

    /// <summary>
    /// Gets the actor state manager for this actor instance.
    /// </summary>
    public IActorStateManager StateManager => Host.StateManager;

    /// <summary>
    /// Gets the proxy factory for communicating with other actors.
    /// </summary>
    public IVirtualActorProxyFactory ProxyFactory => Host.ProxyFactory;

    /// <summary>
    /// Gets the logger for this actor instance.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Called when the actor is activated. Override to perform initialization logic.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous activation.</returns>
    protected internal virtual Task OnActivateAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <summary>
    /// Called when the actor is deactivated. Override to perform cleanup logic.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous deactivation.</returns>
    protected internal virtual Task OnDeactivateAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <summary>
    /// Called before an actor method is invoked. Override to add pre-processing logic.
    /// </summary>
    /// <param name="context">Context about the method being invoked.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected internal virtual Task OnPreActorMethodAsync(ActorMethodContext context, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <summary>
    /// Called after an actor method is invoked. Override to add post-processing logic.
    /// </summary>
    /// <param name="context">Context about the method that was invoked.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected internal virtual Task OnPostActorMethodAsync(ActorMethodContext context, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
