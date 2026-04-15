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
/// Provides context about the current actor method invocation to middleware and lifecycle hooks.
/// </summary>
/// <remarks>
/// <para>
/// This context is passed through the middleware pipeline and is available to all
/// <see cref="IActorMiddleware"/> components, lifecycle observers, and the actor itself.
/// </para>
/// <para>
/// Extensions can store custom data in <see cref="Properties"/> to pass information
/// between middleware components in the pipeline.
/// </para>
/// </remarks>
public sealed class ActorInvocationContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActorInvocationContext"/> class.
    /// </summary>
    /// <param name="actorType">The actor type name.</param>
    /// <param name="actorId">The actor ID.</param>
    /// <param name="methodName">The method being invoked.</param>
    /// <param name="callType">The type of call.</param>
    public ActorInvocationContext(string actorType, VirtualActorId actorId, string methodName, ActorCallType callType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

        ActorType = actorType;
        ActorId = actorId;
        MethodName = methodName;
        CallType = callType;
    }

    /// <summary>
    /// Gets the actor type name.
    /// </summary>
    public string ActorType { get; }

    /// <summary>
    /// Gets the actor ID.
    /// </summary>
    public VirtualActorId ActorId { get; }

    /// <summary>
    /// Gets the name of the method being invoked.
    /// </summary>
    public string MethodName { get; }

    /// <summary>
    /// Gets the type of call (actor method, timer, or reminder).
    /// </summary>
    public ActorCallType CallType { get; }

    /// <summary>
    /// Gets or sets the raw request data (serialized bytes from the caller).
    /// </summary>
    /// <remarks>
    /// Middleware can inspect or transform the request data before it reaches the actor.
    /// </remarks>
    public byte[]? RequestData { get; set; }

    /// <summary>
    /// Gets or sets the raw response data (serialized bytes from the actor).
    /// </summary>
    /// <remarks>
    /// Middleware can inspect or transform the response data before it is returned to the caller.
    /// </remarks>
    public byte[]? ResponseData { get; set; }

    /// <summary>
    /// Gets a mutable dictionary for storing custom properties that flow through
    /// the middleware pipeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this to pass data between middleware components. For example, a tracing
    /// middleware could store span information that a metrics middleware later reads.
    /// </para>
    /// <para>
    /// Convention: Use a fully-qualified type name as the key to avoid collisions
    /// (e.g., <c>"Dapr.AgenticFramework.VectorContext"</c>).
    /// </para>
    /// </remarks>
    public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the reentrancy ID for this invocation, if applicable.
    /// </summary>
    public string? ReentrancyId { get; set; }
}
