// ------------------------------------------------------------------------
// Copyright 2023 The Dapr Authors
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

using System;

namespace Dapr.AspNetCore;

/// <summary>
/// BulkSubscribeAttribute describes options for a bulk subscriber with respect to a topic.
/// It needs to be paired with at least one [Topic] depending on the use case.
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
public class BulkSubscribeAttribute : Attribute, IBulkSubscribeMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BulkSubscribeAttribute" /> class.
    /// </summary>
    /// <param name="topicName">The name of topic.</param>
    /// <param name="maxMessagesCount">The name of the pubsub component to use.</param>
    /// <param name="maxAwaitDurationMs">The topic name.</param>
    public BulkSubscribeAttribute(string topicName, int maxMessagesCount, int maxAwaitDurationMs)
    {
        this.TopicName = topicName;
        this.MaxMessagesCount = maxMessagesCount;
        this.MaxAwaitDurationMs = maxAwaitDurationMs;
    }
        
    /// <summary>
    /// Initializes a new instance of the <see cref="BulkSubscribeAttribute" /> class.
    /// </summary>
    /// <param name="topicName">The name of topic.</param>
    /// <param name="maxMessagesCount">The name of the pubsub component to use.</param>
    public BulkSubscribeAttribute(string topicName, int maxMessagesCount)
    {
        this.TopicName = topicName;
        this.MaxMessagesCount = maxMessagesCount;
    }
        
    /// <summary>
    /// Initializes a new instance of the <see cref="BulkSubscribeAttribute" /> class.
    /// </summary>
    /// <param name="topicName">The name of topic.</param>
    public BulkSubscribeAttribute(string topicName)
    {
        this.TopicName = topicName;
    }

    /// <summary>
    /// Maximum number of messages in a bulk message from the message bus.
    /// </summary>
    public int MaxMessagesCount { get; } = 100;

    /// <summary>
    /// Maximum duration to wait for maxBulkSubCount messages by the message bus
    /// before sending the messages to Dapr.
    /// </summary>
    public int MaxAwaitDurationMs { get; } = 1000;
        
    /// <summary>
    /// The name of the topic to be bulk subscribed.
    /// </summary>
    public string TopicName { get; }
}