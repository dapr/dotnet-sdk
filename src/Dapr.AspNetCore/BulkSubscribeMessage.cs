// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

using System.Collections.Generic;

namespace Dapr.AspNetCore;

/// <summary>
/// Represents a bulk of messages received from the message bus.
/// </summary>
/// <typeparam name="TValue">The type of value contained in the data.</typeparam>
public class BulkSubscribeMessage<TValue>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BulkSubscribeMessage{TValue}"/> class.
    /// </summary>
    public BulkSubscribeMessage()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BulkSubscribeMessage{TValue}"/> class.
    /// </summary>
    /// <param name="entries">A list of entries representing the event and other metadata.</param>
    /// <param name="topic">The name of the pubsub topic.</param>
    /// <param name="metadata">Metadata for the bulk message.</param>
    public BulkSubscribeMessage(List<BulkSubscribeMessageEntry<TValue>> entries, string topic, Dictionary<string, string> metadata)
    {
        this.Entries = entries;
        this.Topic = topic;
        this.Metadata = metadata;
    }

    /// <summary>
    /// A list of entries representing the event and other metadata.
    /// </summary>
    public List<BulkSubscribeMessageEntry<TValue>> Entries { get; set; }
        
    /// <summary>
    /// The name of the pubsub topic.
    /// </summary>
    public string Topic { get; set; }
        
    /// <summary>
    /// Metadata for the bulk message.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }
}