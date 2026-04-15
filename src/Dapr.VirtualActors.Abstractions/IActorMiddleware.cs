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

namespace Dapr.VirtualActors.Runtime;

/// <summary>
/// Defines a middleware component in the actor method invocation pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Actor middleware executes around every actor method invocation, providing
/// cross-cutting concerns like logging, metrics, authorization, or state enrichment.
/// Middleware components are composed into a pipeline and invoked in registration order.
/// </para>
/// <para>
/// Add-on projects (e.g., a hypothetical <c>Dapr.AgenticFramework</c>) can register
/// middleware to observe or modify actor behavior without changes to the core runtime:
/// </para>
/// <code>
/// services.AddDaprVirtualActors(options => { ... })
///     .UseMiddleware&lt;VectorStoreEnrichmentMiddleware&gt;();
/// </code>
/// </remarks>
public interface IActorMiddleware
{
    /// <summary>
    /// Invoked for each actor method call. Call <paramref name="next"/> to continue
    /// the pipeline, or skip it to short-circuit.
    /// </summary>
    /// <param name="context">The context for the current actor method invocation.</param>
    /// <param name="next">
    /// A delegate that invokes the next middleware in the pipeline (or the actor method itself
    /// if this is the last middleware).
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task InvokeAsync(ActorInvocationContext context, ActorMiddlewareDelegate next, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the next step in the actor middleware pipeline.
/// </summary>
/// <param name="context">The invocation context.</param>
/// <param name="cancellationToken">A token to cancel the operation.</param>
/// <returns>A task that represents the asynchronous operation.</returns>
public delegate Task ActorMiddlewareDelegate(ActorInvocationContext context, CancellationToken cancellationToken);
