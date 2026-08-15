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

namespace Dapr;

/// <summary>
/// The JSON property names of the attributes that make up a CloudEvent 1.0 envelope as
/// produced/consumed by Dapr, including the Dapr-specific extension attributes
/// (<c>topic</c>, <c>pubsubname</c>, <c>traceid</c>, <c>traceparent</c>, <c>tracestate</c>).
/// </summary>
/// <remarks>
/// These are the shared source of truth for the wire-format names used by
/// <see cref="Dapr.CloudEvent"/>, the <c>cloudevent.*</c> override metadata keys in
/// <see cref="Dapr.Client.DaprOutboxMetadata"/>, and the ASP.NET Core CloudEvents middleware.
/// See <a href="https://docs.dapr.io/developing-applications/building-blocks/pubsub/pubsub-cloudevents/"/>
/// and the <a href="https://github.com/cloudevents/spec/tree/v1.0">CloudEvents 1.0 spec</a>.
/// </remarks>
internal static class CloudEventPropertyNames
{
    // --- CloudEvents 1.0 core attributes ---

    /// <summary>CloudEvent <c>id</c> attribute (required by the CE spec).</summary>
    public const string Id = "id";

    /// <summary>CloudEvent <c>source</c> attribute (required by the CE spec).</summary>
    public const string Source = "source";

    /// <summary>CloudEvent <c>specversion</c> attribute (required by the CE spec).</summary>
    public const string SpecVersion = "specversion";

    /// <summary>CloudEvent <c>type</c> attribute (required by the CE spec).</summary>
    public const string Type = "type";

    /// <summary>CloudEvent <c>time</c> attribute.</summary>
    public const string Time = "time";

    /// <summary>CloudEvent <c>subject</c> attribute.</summary>
    public const string Subject = "subject";

    /// <summary>CloudEvent <c>datacontenttype</c> attribute.</summary>
    public const string DataContentType = "datacontenttype";

    /// <summary>CloudEvent <c>data</c> attribute.</summary>
    public const string Data = "data";

    /// <summary>CloudEvent <c>data_base64</c> attribute (binary payload, base64 encoded).</summary>
    public const string DataBase64 = "data_base64";

    // --- Dapr envelope extension attributes ---

    /// <summary>Dapr <c>topic</c> attribute on a pub/sub CloudEvent envelope.</summary>
    public const string Topic = "topic";

    /// <summary>Dapr <c>pubsubname</c> attribute on a pub/sub CloudEvent envelope.</summary>
    public const string PubSubName = "pubsubname";

    /// <summary>Dapr <c>traceid</c> attribute (W3C trace identifier).</summary>
    public const string TraceId = "traceid";

    /// <summary>Dapr <c>traceparent</c> attribute (W3C traceparent).</summary>
    public const string TraceParent = "traceparent";

    /// <summary>Dapr <c>tracestate</c> attribute (W3C tracestate).</summary>
    public const string TraceState = "tracestate";
}
