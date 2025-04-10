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

#nullable enable
using System.Collections.Generic;

namespace Dapr.Client;

/// <summary>
/// Class representing an entry in the BulkPublishRequest.
/// </summary>
/// <typeparam name="TValue">The data type of the value.</typeparam>
/// <param name="entryId">A request scoped ID uniquely identifying this entry in the BulkPublishRequest.</param>
/// <param name="eventData">Event to be published.</param>
/// <param name="contentType">Content Type of the event to be published.</param>
/// <param name="metadata">Metadata for the event.</param>
public class BulkPublishEntry<TValue>(string entryId, TValue eventData, string contentType, IReadOnlyDictionary<string, string>? metadata = null)
{
    /// <summary>
    /// The ID uniquely identifying this particular request entry across the request and scoped for this request only.
    /// </summary>
    public string EntryId { get; } = entryId;

    /// <summary>
    /// The event to be published.
    /// </summary>
    public TValue EventData { get; } = eventData;

    /// <summary>
    /// The content type of the event to be published.
    /// </summary>
    public string ContentType { get; } = contentType;

    /// <summary>
    /// The metadata set for this particular event.
    /// Any particular values in this metadata overrides the request metadata present in BulkPublishRequest.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; } = metadata;
}
