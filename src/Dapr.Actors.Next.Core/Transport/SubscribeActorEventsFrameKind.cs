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

namespace Dapr.Actors.Next.Core.Transport;

/// <summary>
/// Identifies an SubscribeActorEvents stream frame kind.
/// </summary>
public enum SubscribeActorEventsFrameKind
{
    /// <summary>
    /// Actor-type advertisement sent by the app.
    /// </summary>
    RegisteredActors = 0,

    /// <summary>
    /// Actor method invocation callback.
    /// </summary>
    Invoke = 1,

    /// <summary>
    /// Actor reminder callback.
    /// </summary>
    Reminder = 2,

    /// <summary>
    /// Actor timer callback.
    /// </summary>
    Timer = 3,

    /// <summary>
    /// Actor deactivation callback.
    /// </summary>
    Deactivate = 4,
}
