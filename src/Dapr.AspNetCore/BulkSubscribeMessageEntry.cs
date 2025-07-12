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
/// Represents a single event from a bulk of messages sent by the message bus.
/// </summary>
/// <typeparam name="TValue">The type of value contained in the data.</typeparam>
public class BulkSubscribeMessageEntry<TValue>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BulkSubscribeMessageEntry{TValue}"/> class.
    /// </summary>
    public BulkSubscribeMessageEntry() {
    }
        
    /// <summary>
    /// Initializes a new instance of the <see cref="BulkSubscribeMessageEntry{TValue}"/> class.
    /// </summary>
    /// <param name="entryId">A unique identifier for the event.</param>
    /// <param name="contentType">Content type of the event.</param>
    /// <param name="metadata">Metadata for the event.</param>
    /// <param name="eventData">The pubsub event.</param>
    public BulkSubscribeMessageEntry(string entryId, string contentType, Dictionary<string, string> metadata, 
        TValue eventData)
    {
        this.EntryId = entryId;
        this.ContentType = contentType;
        this.Metadata = metadata;
        this.Event = eventData;
    }

    /// <summary>
    /// A unique identifier for the event.
    /// </summary>
    public string EntryId { get; set; }
        
    /// <summary>
    /// Content type of the event.
    /// </summary>
    public string ContentType { get; set; }
        
    /// <summary>
    /// Metadata for the event.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }
        
    /// <summary>
    /// The pubsub event.
    /// </summary>
    public TValue Event { get; set; }
        
}