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

namespace Dapr.Messaging.Generators.Models;

/// <summary>
/// Model for a single <c>[DaprTopic]</c> attribute applied to a handler class.
/// One <see cref="TopicHandlerModel"/> corresponds to one dispatcher to emit.
/// </summary>
internal sealed class TopicHandlerModel
{
    /// <summary>Fully-qualified handler type name.</summary>
    public string HandlerFqn { get; set; } = string.Empty;

    /// <summary>Fully-qualified message type name.</summary>
    public string MessageFqn { get; set; } = string.Empty;

    /// <summary>Whether the handler implements <c>ITopicHandler&lt;TMessage, TResult&gt;</c>.</summary>
    public bool IsResponseVariant { get; set; }

    /// <summary>Fully-qualified result type name (when <see cref="IsResponseVariant"/> is true).</summary>
    public string? ResultFqn { get; set; }

    /// <summary>pubsubname from the attribute constructor.</summary>
    public string PubsubName { get; set; } = string.Empty;

    /// <summary>topic from the attribute constructor.</summary>
    public string TopicName { get; set; } = string.Empty;

    /// <summary>Application route used for HTTP delivery.</summary>
    public string Route { get; set; } = string.Empty;

    /// <summary>Delivery mode (Streaming/Programmatic) as an int for emission.</summary>
    public string Delivery { get; set; } = "Streaming";

    /// <summary>CEL match expression (may be null).</summary>
    public string? Match { get; set; }

    /// <summary>Routing priority (0 if unset).</summary>
    public int Priority { get; set; }

    /// <summary>Dead-letter topic (may be null).</summary>
    public string? DeadLetterTopic { get; set; }

    /// <summary>EnableRawPayload tri-state (null if unset).</summary>
    public bool? EnableRawPayload { get; set; }

    /// <summary>Bulk subscribe flag.</summary>
    public bool BulkSubscribe { get; set; }

    /// <summary>Max messages count for bulk subscribe.</summary>
    public int MaxMessagesCount { get; set; } = 100;

    /// <summary>Max await duration (ms) for bulk subscribe.</summary>
    public int MaxAwaitDurationMs { get; set; } = 1000;

    /// <summary>Correlated metadata key/value pairs.</summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>A stable dispatcher class name for this model.</summary>
    public string DispatcherClassName { get; set; } = string.Empty;
}
