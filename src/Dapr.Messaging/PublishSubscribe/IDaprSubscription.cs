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
/// Represents a Dapr pub/sub subscription whose lifetime can be observed and disposed.
/// </summary>
public interface IDaprSubscription : IAsyncDisposable
{
    /// <summary>
    /// A task that completes when the subscription's background processing finishes.
    /// Faults with a <see cref="DaprException"/> if a background task errors and no
    /// <see cref="DaprSubscriptionOptions.ErrorHandler"/> is configured. If the handler
    /// itself throws, the task faults with an <see cref="AggregateException"/> combining
    /// the original fault and the handler failure.
    /// </summary>
    Task Completion { get; }
}
