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

namespace Dapr.Client;

/// <summary>
/// Well-known metadata keys used by Dapr's transactional outbox feature on
/// state transaction operations and by the <see cref="OutboxTransactionBuilder"/>.
/// </summary>
/// <remarks>
/// See <a href="https://docs.dapr.io/developing-applications/building-blocks/state-management/howto-outbox/">
/// How-To: Enable the transactional outbox pattern</a> for details.
/// </remarks>
public static class DaprOutboxMetadata
{
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
    public const string CloudEventId = "cloudevent.id";

    /// <summary>
    /// Overrides the CloudEvent <c>source</c> field on the published outbox event.
    /// </summary>
    public const string CloudEventSource = "cloudevent.source";

    /// <summary>
    /// Overrides the CloudEvent <c>type</c> field on the published outbox event.
    /// </summary>
    public const string CloudEventType = "cloudevent.type";

    /// <summary>
    /// Overrides the CloudEvent <c>subject</c> field on the published outbox event.
    /// </summary>
    public const string CloudEventSubject = "cloudevent.subject";

    /// <summary>
    /// Overrides the CloudEvent <c>datacontenttype</c> field on the published outbox event.
    /// </summary>
    public const string CloudEventDataContentType = "cloudevent.datacontenttype";
}
