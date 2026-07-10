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

using Dapr.Actors.Next.Core;

namespace Dapr.Actors.Next.Core.Transport;

/// <summary>
/// Represents one response frame sent on the SubscribeActorEvents stream.
/// </summary>
public sealed record SubscribeActorEventsResponse(
    string Id,
    SubscribeActorEventsFrameKind Kind,
    ReadOnlyMemory<byte> Payload,
    IReadOnlyDictionary<string, string> Headers,
    string? FailureMessage = null,
    SubscribeActorEventsInitialConfig? InitialConfig = null,
    bool Error = false,
    bool Cancel = false,
    uint FailureCode = SubscribeActorEventsResponse.UnknownStatusCode)
{
    /// <summary>
    /// Default gRPC status code used for system failures when a more specific code is unavailable.
    /// </summary>
    public const uint UnknownStatusCode = 2;

    /// <summary>
    /// Creates an actor-type advertisement frame.
    /// </summary>
    public static SubscribeActorEventsResponse RegisteredActors(IReadOnlyList<string> actorTypes)
    {
        var payload = System.Text.Encoding.UTF8.GetBytes(string.Join('\n', actorTypes));
        return new SubscribeActorEventsResponse(Guid.NewGuid().ToString("N"), SubscribeActorEventsFrameKind.RegisteredActors, payload, ActorHeaders.Empty);
    }

    /// <summary>
    /// Creates an actor-type advertisement frame with runtime configuration.
    /// </summary>
    public static SubscribeActorEventsResponse RegisteredActors(IReadOnlyList<string> actorTypes, SubscribeActorEventsInitialConfig config)
    {
        var payload = System.Text.Encoding.UTF8.GetBytes(string.Join('\n', actorTypes));
        return new SubscribeActorEventsResponse(
            Guid.NewGuid().ToString("N"),
            SubscribeActorEventsFrameKind.RegisteredActors,
            payload,
            ActorHeaders.Empty,
            InitialConfig: config);
    }

    /// <summary>
    /// Creates a system failure response for a callback that could not be processed.
    /// </summary>
    public static SubscribeActorEventsResponse Failed(string id, string message, uint code = UnknownStatusCode) =>
        new(id, SubscribeActorEventsFrameKind.Invoke, ReadOnlyMemory<byte>.Empty, ActorHeaders.Empty, message, FailureCode: code);
}

/// <summary>
/// Runtime options advertised with the initial actor event stream registration.
/// </summary>
public sealed record SubscribeActorEventsInitialConfig(
    TimeSpan ActorIdleTimeout,
    TimeSpan DrainOngoingCallTimeout,
    bool DrainRebalancedActors,
    bool EnableReentrancy,
    int MaxReentrantDepth);
