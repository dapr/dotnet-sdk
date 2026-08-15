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

using System;
using System.Text.Json.Serialization;
using Dapr.Client;

namespace Dapr;

/// <summary>
/// Represents a CloudEvent without data.
/// </summary>        
public class CloudEvent
{
    /// <summary>
    /// CloudEvent 'id' attribute (required by the CloudEvents 1.0 spec). When omitted on
    /// publish, Dapr generates a value for the envelope.
    /// </summary>
    [JsonPropertyName(CloudEventPropertyNames.Id)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Id { get; init; }

    /// <summary>
    /// CloudEvent 'source' attribute (required by the CloudEvents 1.0 spec).
    /// </summary>
    [JsonPropertyName(CloudEventPropertyNames.Source)]
    public Uri Source { get; init; }

    /// <summary>
    /// CloudEvent 'specversion' attribute (required by the CloudEvents 1.0 spec). When
    /// omitted on publish, Dapr populates the envelope with the spec version in use.
    /// </summary>
    [JsonPropertyName(CloudEventPropertyNames.SpecVersion)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string SpecVersion { get; init; }

    /// <summary>
    /// CloudEvent 'type' attribute (required by the CloudEvents 1.0 spec).
    /// </summary>
    [JsonPropertyName(CloudEventPropertyNames.Type)]
    public string Type { get; init; }

    /// <summary>
    /// CloudEvent 'time' attribute. When omitted on publish, Dapr stamps the envelope
    /// with the time of the publish operation.
    /// </summary>
    [JsonPropertyName(CloudEventPropertyNames.Time)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? Time { get; init; }

    /// <summary>
    /// CloudEvent 'subject' attribute.
    /// </summary>
    [JsonPropertyName(CloudEventPropertyNames.Subject)]
    public string Subject { get; init; }

    /// <summary>
    /// W3C 'traceid' attribute as carried on the Dapr CloudEvent envelope. When omitted on
    /// publish, Dapr propagates the ambient trace identifier.
    /// </summary>
    [JsonPropertyName(CloudEventPropertyNames.TraceId)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string TraceId { get; init; }

    /// <summary>
    /// W3C 'traceparent' attribute as carried on the Dapr CloudEvent envelope. When omitted
    /// on publish, Dapr propagates the ambient traceparent.
    /// </summary>
    [JsonPropertyName(CloudEventPropertyNames.TraceParent)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string TraceParent { get; init; }

    /// <summary>
    /// W3C 'tracestate' attribute as carried on the Dapr CloudEvent envelope. When omitted
    /// on publish, Dapr propagates the ambient tracestate.
    /// </summary>
    [JsonPropertyName(CloudEventPropertyNames.TraceState)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string TraceState { get; init; }
}

/// <summary>
/// Represents a CloudEvent with typed data.
/// </summary>
public class CloudEvent<TData>(TData data) : CloudEvent
{
    /// <summary>
    /// CloudEvent 'data' content.
    /// </summary>
    [JsonPropertyName(CloudEventPropertyNames.Data)]
    public TData Data { get; } = data;

    /// <summary>
    /// Gets event data.
    /// </summary>
    [JsonPropertyName(CloudEventPropertyNames.DataContentType)]
    public string DataContentType { get; } = Constants.ContentTypeApplicationJson;
}
