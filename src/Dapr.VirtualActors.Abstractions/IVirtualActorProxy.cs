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
/// Represents a weakly-typed proxy for invoking methods on a virtual actor dynamically.
/// </summary>
public interface IVirtualActorProxy
{
    /// <summary>
    /// Gets the actor ID this proxy targets.
    /// </summary>
    VirtualActorId ActorId { get; }

    /// <summary>
    /// Gets the actor type name this proxy targets.
    /// </summary>
    string ActorType { get; }

    /// <summary>
    /// Invokes a method on the actor that returns no result.
    /// </summary>
    /// <param name="methodName">The name of the actor method to invoke.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task InvokeMethodAsync(string methodName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes a method on the actor with a request payload that returns no result.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request payload.</typeparam>
    /// <param name="methodName">The name of the actor method to invoke.</param>
    /// <param name="data">The request payload to send to the actor method.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task InvokeMethodAsync<TRequest>(string methodName, TRequest data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes a method on the actor that returns a result.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="methodName">The name of the actor method to invoke.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The result of the actor method invocation.</returns>
    Task<TResponse> InvokeMethodAsync<TResponse>(string methodName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes a method on the actor with a request payload that returns a result.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request payload.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="methodName">The name of the actor method to invoke.</param>
    /// <param name="data">The request payload to send to the actor method.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The result of the actor method invocation.</returns>
    Task<TResponse> InvokeMethodAsync<TRequest, TResponse>(string methodName, TRequest data, CancellationToken cancellationToken = default);
}
