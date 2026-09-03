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

#nullable enable

using Dapr;

namespace Dapr.Client;

/// <summary>
/// Well-known metadata keys used by Dapr's transactional outbox feature on
/// state transaction operations and by the <see cref="OutboxTransactionBuilder"/>.
/// </summary>
/// <remarks>
/// The <c>cloudevent.*</c> keys override the corresponding CloudEvent attribute on the
/// published outbox event and are derived from the shared <see cref="CloudEventPropertyNames"/>
/// so there is a single source of truth for the wire-format names.
/// See <a href="https://docs.dapr.io/developing-applications/building-blocks/state-management/howto-outbox/">
/// How-To: Enable the transactional outbox pattern</a> for details.
/// </remarks>
public static class DaprOutboxMetadata
{
    /// <summary>
    /// Prefix shared by every CloudEvent override metadata key. Centralized here so the
    /// <c>cloudevent.*</c> keys below cannot drift from the documented prefix.
    /// </summary>
    private const string CloudEventMetadataPrefix = "cloudevent.";

    /// <summary>
    /// Marks a <see cref="StateTransactionRequest"/> as an outbox projection.
    /// The item is not written to the state store; its value is used as the payload
    /// published to the pub/sub topic configured on the state store component.
    /// </summary>
    public const string Projection = "outbox.projection";

    /// <summary>
    /// Value used with <see cref="Projection"/> to enable the projection behavior.
    /// </summary>
    public const string ProjectionEnabled = "true";

    /// <summary>
    /// Overrides the CloudEvent <c>id</c> field on the published outbox event.
    /// </summary>
    public const string CloudEventId = CloudEventMetadataPrefix + CloudEventPropertyNames.Id;

    /// <summary>
    /// Overrides the CloudEvent <c>source</c> field on the published outbox event.
    /// </summary>
    public const string CloudEventSource = CloudEventMetadataPrefix + CloudEventPropertyNames.Source;

    /// <summary>
    /// Overrides the CloudEvent <c>type</c> field on the published outbox event.
    /// </summary>
    public const string CloudEventType = CloudEventMetadataPrefix + CloudEventPropertyNames.Type;

    /// <summary>
    /// Overrides the CloudEvent <c>subject</c> field on the published outbox event.
    /// </summary>
    public const string CloudEventSubject = CloudEventMetadataPrefix + CloudEventPropertyNames.Subject;

    /// <summary>
    /// Overrides the CloudEvent <c>datacontenttype</c> field on the published outbox event.
    /// </summary>
    public const string CloudEventDataContentType = CloudEventMetadataPrefix + CloudEventPropertyNames.DataContentType;

    /// <summary>
    /// Overrides the CloudEvent <c>traceid</c> field on the published outbox event.
    /// </summary>
    /// <remarks>
    /// Overriding trace identifiers may interfere with distributed tracing and report
    /// inconsistent results in tracing tools. Prefer OpenTelemetry for trace propagation.
    /// </remarks>
    public const string CloudEventTraceId = CloudEventMetadataPrefix + CloudEventPropertyNames.TraceId;

    /// <summary>
    /// Overrides the CloudEvent <c>traceparent</c> field on the published outbox event.
    /// </summary>
    /// <remarks>
    /// Overriding trace identifiers may interfere with distributed tracing and report
    /// inconsistent results in tracing tools. Prefer OpenTelemetry for trace propagation.
    /// </remarks>
    public const string CloudEventTraceParent = CloudEventMetadataPrefix + CloudEventPropertyNames.TraceParent;

    /// <summary>
    /// Overrides the CloudEvent <c>tracestate</c> field on the published outbox event.
    /// </summary>
    /// <remarks>
    /// Overriding trace identifiers may interfere with distributed tracing and report
    /// inconsistent results in tracing tools. Prefer OpenTelemetry for trace propagation.
    /// </remarks>
    public const string CloudEventTraceState = CloudEventMetadataPrefix + CloudEventPropertyNames.TraceState;
}
