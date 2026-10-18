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
/// Typed options for a publish operation, replacing the loose <c>Dictionary&lt;string, string&gt;</c>
/// metadata bag. Any <c>cloudevent.*</c> override supplied here is forwarded to the Dapr runtime
/// as publish metadata and takes precedence over values populated by the runtime.
/// </summary>
public sealed class PublishOptions
{
    /// <summary>
    /// Metadata key/value pairs forwarded to the pub/sub component and the Dapr runtime.
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// Overrides the auto-detected content type of the published payload.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// CloudEvent <c>id</c> attribute.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// CloudEvent <c>source</c> attribute.
    /// </summary>
    public Uri? Source { get; init; }

    /// <summary>
    /// CloudEvent <c>type</c> attribute.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// CloudEvent <c>subject</c> attribute.
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>
    /// W3C trace parent.
    /// </summary>
    public string? TraceParent { get; init; }

    /// <summary>
    /// W3C trace state.
    /// </summary>
    public string? TraceState { get; init; }
}
