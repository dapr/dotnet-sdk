// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using System.Text.Json.Serialization;
using Dapr.Common.JsonConverters;

namespace Dapr.Messaging.PublishSubscribe;

/// <summary>
/// Represents a CloudEvent without data.
/// </summary>
/// <param name="Source">The context in which an event happened, e.g. the type of an event source.</param>
/// <param name="Type">Describes the type of event related to the originating occurrence.</param>
public record CloudEvent(
    [property: JsonPropertyName("source")] Uri Source, 
    [property: JsonPropertyName("type")] string Type)
{
    /// <summary>
    /// The subject of the event in the context of the event producer (identified by <see cref="Source"/>).
    /// </summary>
    [JsonPropertyName("subject")]
    public string? Subject { get; init; }

    /// <summary>
    /// The version of the CloudEvents specification which the event uses.
    /// </summary>
    /// <remarks>
    /// While this SDK implements specification 1.0.2, this value only has the major and minor values included allowing
    /// for "patch" changes that don't change this property's value in the serialization.
    /// </remarks>
    [JsonPropertyName("specversion")]
    public static string SpecVersion => "1.0";

    /// <summary>
    /// The timestamp of when the occurrence happened.
    /// </summary>
    [JsonPropertyName("time")]
    [JsonConverter(typeof(Rfc3389JsonConverter))]
    public DateTimeOffset? Time { get; init; } = null;
}

/// <summary>
/// Represents a CloudEvent with typed data.
/// </summary>
/// <param name="Source">The context in which an event happened, e.g. the type of an event source.</param>
/// <param name="Type">Describes the type of event related to the originating occurrence.</param>
/// <param name="Data">Domain-specific information about the event occurrence.</param>
public record CloudEvent<TData>(
    Uri Source, 
    string Type, 
    [property: JsonPropertyName("data")] TData Data) : CloudEvent(Source, Type)
{
    /// <summary>
    /// Content type of the data value.
    /// </summary>
    [JsonPropertyName("datacontenttype")]
    public string DataContentType { get; init; } = "application/json";
}
