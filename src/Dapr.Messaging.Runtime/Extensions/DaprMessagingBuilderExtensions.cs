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

using Dapr.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Explicit (non-source-generated) topic registration extensions for the <c>Dapr.Messaging</c> stack.
/// Prefer annotating <see cref="Dapr.Messaging.ITopicHandler{TMessage}"/> implementations with
/// <c>[DaprTopic]</c> and calling the source-generated <c>AddDaprMessaging</c>; these methods are
/// provided for runtime-only registrations.
/// </summary>
public static class DaprMessagingBuilderExtensions
{
    /// <summary>
    /// Registers a handler type for a topic. The handler must implement
    /// <see cref="Dapr.Messaging.ITopicHandler{TMessage}"/> (or
    /// <see cref="Dapr.Messaging.ITopicHandler{TMessage, TResult}"/>).
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    public static IDaprMessagingBuilder AddTopic<THandler>(
        this IDaprMessagingBuilder builder,
        string pubsubName,
        string topicName,
        Action<TopicSubscriptionDescriptor>? configure = null)
        where THandler : class
    {
        var descriptor = new Dapr.Messaging.TopicSubscriptionDescriptor
        {
            PubsubName = pubsubName,
            TopicName = topicName,
            HandlerType = typeof(THandler),
            Route = topicName,
        };
        configure?.Invoke(descriptor);
        builder.Services.AddTransient<THandler>();
        return builder;
    }
}
