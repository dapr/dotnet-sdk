// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

namespace Dapr.VirtualActors;

/// <summary>
/// Provides context about an actor method invocation, used in pre- and post-method hooks.
/// </summary>
/// <param name="MethodName">The name of the method being invoked.</param>
/// <param name="CallType">The type of call (actor method, timer, or reminder).</param>
public sealed record ActorMethodContext(string MethodName, ActorCallType CallType);

/// <summary>
/// Specifies the type of actor method invocation.
/// </summary>
public enum ActorCallType
{
    /// <summary>
    /// A standard actor method invocation.
    /// </summary>
    ActorMethod = 0,

    /// <summary>
    /// A timer callback invocation.
    /// </summary>
    TimerCallback = 1,

    /// <summary>
    /// A reminder callback invocation.
    /// </summary>
    ReminderCallback = 2,
}
