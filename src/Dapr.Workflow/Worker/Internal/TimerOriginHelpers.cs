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

using Dapr.DurableTask.Protobuf;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace Dapr.Workflow.Worker.Internal;

/// <summary>
/// Static helpers for timer origin metadata and optional-timer recognition.
/// Kept separate from <see cref="WorkflowOrchestrationContext"/> to avoid
/// mixing protobuf types into the context's public surface.
/// </summary>
internal static class TimerOriginHelpers
{
    /// <summary>
    /// Sentinel fireAt value for indefinite external event timers.
    /// Must be exactly 9999-12-31T23:59:59.999999999Z.
    /// </summary>
    internal static readonly Timestamp ExternalEventIndefiniteFireAt =
        new()
        {
            Seconds = 253402300799, // 9999-12-31T23:59:59Z
            Nanos = 999999999
        };

    /// <summary>
    /// Sets the appropriate origin field on a <see cref="CreateTimerAction"/> based on the
    /// runtime type of the supplied origin message.
    /// </summary>
    internal static void SetTimerOrigin(CreateTimerAction action, IMessage origin)
    {
        switch (origin)
        {
            case TimerOriginCreateTimer createTimer:
                action.OriginCreateTimer = createTimer;
                break;
            case TimerOriginExternalEvent externalEvent:
                action.OriginExternalEvent = externalEvent;
                break;
            case TimerOriginActivityRetry activityRetry:
                action.OriginActivityRetry = activityRetry;
                break;
            case TimerOriginChildWorkflowRetry childWorkflowRetry:
                action.OriginChildWorkflowRetry = childWorkflowRetry;
                break;
        }
    }

    /// <summary>
    /// Determines whether a <see cref="OrchestratorAction"/> is an optional external event timer
    /// (sentinel fireAt + ExternalEvent origin).
    /// </summary>
    internal static bool IsOptionalExternalEventTimerAction(OrchestratorAction action)
    {
        return action.CreateTimer is { } timer
               && timer.OriginCase == CreateTimerAction.OriginOneofCase.OriginExternalEvent
               && timer.FireAt != null
               && timer.FireAt.Equals(ExternalEventIndefiniteFireAt);
    }

    /// <summary>
    /// Determines whether a <see cref="TimerCreatedEvent"/> is an optional external event timer
    /// (sentinel fireAt + ExternalEvent origin).
    /// </summary>
    internal static bool IsOptionalExternalEventTimerCreatedEvent(TimerCreatedEvent timerCreated)
    {
        return timerCreated.OriginCase == TimerCreatedEvent.OriginOneofCase.OriginExternalEvent
               && timerCreated.FireAt != null
               && timerCreated.FireAt.Equals(ExternalEventIndefiniteFireAt);
    }
}
