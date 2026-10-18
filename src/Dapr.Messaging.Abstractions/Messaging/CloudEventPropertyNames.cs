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

namespace Dapr.Messaging;

/// <summary>
/// The JSON property names of the attributes that make up a CloudEvent 1.0 envelope as
/// produced/consumed by Dapr, including the Dapr-specific extension attributes
/// (<c>topic</c>, <c>pubsubname</c>, <c>traceid</c>, <c>traceparent</c>, <c>tracestate</c>).
/// </summary>
internal static class CloudEventPropertyNames
{
    public const string Id = "id";
    public const string Source = "source";
    public const string SpecVersion = "specversion";
    public const string Type = "type";
    public const string Time = "time";
    public const string Subject = "subject";
    public const string DataContentType = "datacontenttype";
    public const string Data = "data";
    public const string DataBase64 = "data_base64";

    public const string Topic = "topic";
    public const string PubSubName = "pubsubname";
    public const string TraceId = "traceid";
    public const string TraceParent = "traceparent";
    public const string TraceState = "tracestate";
}
