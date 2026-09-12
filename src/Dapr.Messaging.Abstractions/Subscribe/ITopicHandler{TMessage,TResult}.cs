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

namespace Dapr.Messaging;

/// <summary>
/// A handler for a Dapr pub/sub topic message that produces a typed result forwarded back to the runtime
/// (for example, when using the gRPC app-callback bulk path with per-entry responses).
/// </summary>
/// <typeparam name="TMessage">The type of the deserialized message payload.</typeparam>
/// <typeparam name="TResult">The type of the result returned by the handler.</typeparam>
public interface ITopicHandler<TMessage, TResult>
{
    /// <summary>
    /// Handles a single topic message and returns a typed result.
    /// </summary>
    /// <param name="message">The deserialized message payload.</param>
    /// <param name="context">The context describing the delivery.</param>
    /// <param name="cancellationToken">A token to cancel the handling operation.</param>
    /// <returns>The typed result of the handling operation.</returns>
    Task<TResult> HandleAsync(TMessage message, TopicContext context, CancellationToken cancellationToken);
}
