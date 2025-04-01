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
    /// CloudEvent 'source' attribute.
    /// </summary>
    [JsonPropertyName("source")]
    public Uri Source { get; init; }

    /// <summary>
    /// CloudEvent 'type' attribute.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; }

    /// <summary>
    /// CloudEvent 'subject' attribute.
    /// </summary>
    [JsonPropertyName("subject")]
    public string Subject { get; init; }
}

/// <summary>
/// Represents a CloudEvent with typed data.
/// </summary>
public class CloudEvent<TData>(TData data) : CloudEvent
{
    /// <summary>
    /// CloudEvent 'data' content.
    /// </summary>
    public TData Data { get; } = data;

    /// <summary>
    /// Gets event data.
    /// </summary>
    [JsonPropertyName("datacontenttype")]
    public string DataContentType { get; } = Constants.ContentTypeApplicationJson;
}
