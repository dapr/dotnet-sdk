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

namespace Dapr.Actors.Next.Streams;

/// <summary>
/// Opens Dapr pub/sub streaming subscriptions for actor stream delivery.
/// </summary>
public interface IActorStreamSubscriptionSource
{
    /// <summary>
    /// Subscribes to a topic stream and yields events that the component has assigned to this app instance.
    /// </summary>
    IAsyncEnumerable<ActorStreamEvent> SubscribeAsync(ActorStreamSubscription subscription, CancellationToken cancellationToken = default);
}
