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
/// Contract implemented by the per-handler dispatchers emitted by the
/// <c>Dapr.Messaging.Generators</c> source generator. Implemented by generated code only.
/// </summary>
/// <remarks>
/// Public (rather than internal) because generated dispatchers live in the consumer's assembly,
/// which cannot be granted <c>InternalsVisibleTo</c> at Abstractions build time.
/// </remarks>
public interface ITopicDispatcher
{
    /// <summary>The compile-time descriptor for the subscription this dispatcher serves.</summary>
    TopicSubscriptionDescriptor Descriptor { get; }

    /// <summary>
    /// Dispatches a raw payload to the handler resolved from <paramref name="serviceProvider"/>.
    /// </summary>
    /// <param name="payload">The raw, UTF-8 JSON payload of the message.</param>
    /// <param name="context">The delivery context.</param>
    /// <param name="serviceProvider">The scoped service provider used to resolve the handler.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The action to take on the message.</returns>
    Task<TopicResponseAction> DispatchAsync(byte[] payload, TopicContext context, IServiceProvider serviceProvider, CancellationToken ct);
}
