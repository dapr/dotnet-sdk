// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

namespace Dapr.Messaging.PublishSubscribe;

/// <summary>
/// A delegate that handles errors occurring during an active subscription.
/// </summary>
/// <param name="exception">The <see cref="DaprException"/> wrapping the original error.</param>
/// <remarks>
/// This handler is invoked on a thread pool thread. Implementations should be thread-safe.
/// If the returned task faults, the handler's exception is combined with the original fault and
/// surfaced as an <see cref="AggregateException"/> on the next call to <c>SubscribeAsync</c>.
/// </remarks>
public delegate Task SubscriptionErrorHandler(DaprException exception);
