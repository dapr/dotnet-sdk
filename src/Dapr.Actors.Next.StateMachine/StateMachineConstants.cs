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

namespace Dapr.Actors.Next.StateMachine;

/// <summary>
/// Shared names used by the state-machine runtime.
/// </summary>
public static class StateMachineConstants
{
    /// <summary>
    /// Persisted state slot containing the current state, data, and durable deferred events.
    /// </summary>
    public const string EnvelopeStateName = "__stateMachine";

    /// <summary>
    /// Test-inspection state slot containing the current enum state.
    /// </summary>
    public const string CurrentStateStateName = "__currentState";

    /// <summary>
    /// Test-inspection state slot containing the current extended data.
    /// </summary>
    public const string DataStateName = "__data";

    /// <summary>
    /// Reserved actor operation used for state-machine timer callbacks.
    /// </summary>
    public const string TimerOperationName = "__stateMachineTimer";

    /// <summary>
    /// Reserved timer name used by declarative state timeouts.
    /// </summary>
    public const string StateTimeoutTimerName = "__stateTimeout";
}
