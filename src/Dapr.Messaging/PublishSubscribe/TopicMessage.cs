// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

using Google.Protobuf.WellKnownTypes;

namespace Dapr.Messaging.PublishSubscribe;

/// <summary>
/// A message retrieved from a Dapr publish/subscribe topic.
/// </summary>
public sealed record TopicMessage
{
    /// <summary>
    /// The unique identifier of the topic message.
    /// </summary>
    public string Id { get; init; } = default!;

    /// <summary>
    /// Identifies the context in which an event happened, such as the organization publishing the
    /// event or the process that produced the event. The exact syntax and semantics behind the data
    /// encoded in the URI is defined by the event producer.
    /// </summary>
    public string Source { get; init; } = default!;

    /// <summary>
    /// The type of event related to the originating occurrence.
    /// </summary>
    public string Type { get; init; } = default!;

    /// <summary>
    /// The spec version of the CloudEvents specification.
    /// </summary>
    public string SpecVersion { get; init; } = default!;

    /// <summary>
    /// The content type of the data.
    /// </summary>
    public string DataContentType { get; init; } = default!;

    /// <summary>
    /// The content of the event.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; init; }

    /// <summary>
    /// The name of the topic.
    /// </summary>
    public string Topic { get; init; } = default!;

    /// <summary>
    /// The name of the Dapr publish/subscribe component.
    /// </summary>
    public string PubSubName { get; init; } = default!;

    /// <summary>
    /// The matching path from the topic subscription/routes (if specified) for this event.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// A map of additional custom properties sent to the app. These are considered to be cloud event extensions.
    /// </summary>
    public Dictionary<string, Value> Extensions { get; init; } = new();
}
