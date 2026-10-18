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
/// Describes how a topic subscription is delivered to the application by the Dapr runtime.
/// </summary>
public enum DeliveryMode
{
    /// <summary>
    /// The streaming approach: the application opens a bidirectional gRPC stream to the sidecar via
    /// <c>SubscribeTopicEventsAlpha1</c> and pulls messages.
    /// </summary>
    Streaming,

    /// <summary>
    /// The programmatic approach: the sidecar pushes events to the application's <c>AppCallback</c> gRPC service
    /// (<c>ListTopicSubscriptions</c> at startup, <c>OnTopicEvent</c> per message).
    /// </summary>
    Programmatic,

    /// <summary>
    /// The HTTP approach: the sidecar discovers the subscription from <c>/dapr/subscribe</c>
    /// and pushes events to the configured application route.
    /// </summary>
    Http,
}
