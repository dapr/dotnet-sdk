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

using Dapr.Messaging.PublishSubscribe;

namespace Dapr.Messaging;

/// <summary>
/// A handler for a Dapr pub/sub topic message.
/// </summary>
/// <typeparam name="TMessage">The type of the deserialized message payload.</typeparam>
/// <remarks>
/// Implementations are registered with the DI container with whatever lifetime the application chooses;
/// constructor injection is fully supported. The source generator discovers implementations annotated
/// with <c>[DaprTopic]</c> at compile time and emits a dispatcher that resolves the handler from
/// <see cref="System.IServiceProvider"/> on each invocation.
/// </remarks>
public interface ITopicHandler<TMessage>
{
    /// <summary>
    /// Handles a single topic message.
    /// </summary>
    /// <param name="message">The deserialized message payload.</param>
    /// <param name="context">The context describing the delivery.</param>
    /// <param name="cancellationToken">A token to cancel the handling operation.</param>
    /// <returns>The action to take on the message once handling completes.</returns>
    Task<TopicResponseAction> HandleAsync(TMessage message, TopicContext context, CancellationToken cancellationToken);
}
