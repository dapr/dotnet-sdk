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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Dapr.DurableTask.Protobuf;

namespace Dapr.Workflow.Worker;

/// <summary>
/// Restores workflow trace context as <see cref="Activity.Current"/> for workflow and activity dispatch.
/// </summary>
internal static class WorkflowTrace
{
    private static readonly ActivitySource WorkflowActivitySource = new("Dapr.Workflow");

    public static TraceActivityScope StartActivityTrace(ActivityRequest request)
    {
        var previous = Activity.Current;
        var activity = StartActivityFromRequest(request);
        return new TraceActivityScope(activity, previous);
    }

    public static TraceActivityScope StartOrchestrationTrace(IEnumerable<HistoryEvent> events)
    {
        // WorkflowRequest does not carry trace context directly. It is stored in workflow history on
        // the ExecutionStarted event, so recover it from the available events.
        var parentTraceContext = events
            .Reverse()
            .FirstOrDefault(e => e.ExecutionStarted?.ParentTraceContext != null)
            ?.ExecutionStarted
            ?.ParentTraceContext;

        var previous = Activity.Current;
        var activity = StartActivityFromTraceContext(
            parentTraceContext,
            "WorkflowOrchestrationTurn",
            emitSpan: false);
        return new TraceActivityScope(activity, previous);
    }

    public static void SetCurrentError(Exception exception) =>
        Activity.Current?.SetStatus(ActivityStatusCode.Error);

    private static Activity? StartActivityFromRequest(ActivityRequest request) =>
        StartActivityFromTraceContext(request.ParentTraceContext, $"WorkflowActivity {request.Name}");

    private static Activity? StartActivityFromTraceContext(
        TraceContext? traceContext,
        string activityName,
        bool emitSpan = true)
    {
        var traceParent = traceContext?.TraceParent;
        if (string.IsNullOrEmpty(traceParent))
            return null;

        var traceState = traceContext?.TraceState;
        if (emitSpan && ActivityContext.TryParse(traceParent, traceState, isRemote: true, out var parentCtx))
        {
            // Prefer ActivitySource so registered telemetry listeners (e.g. OpenTelemetry) receive the span.
            var started = WorkflowActivitySource.StartActivity(
                name: activityName,
                kind: ActivityKind.Internal,
                parentContext: parentCtx,
                []);
            if (started != null)
                return started;

            // ActivitySource.StartActivity returns null when no listener is registered for "Dapr.Workflow".
            // Fall through to ensure Activity.Current is always non-null inside user activity code
            // and workflow orchestration code,
            // regardless of whether OpenTelemetry or another telemetry listener is configured.
        }

        // Always create an Activity directly (not via ActivitySource) so that Activity.Current is
        // non-null in user activity code and workflow orchestration code even when no telemetry listener is registered.
        var act = new Activity(activityName);
        act.SetParentId(traceParent);
        if (!string.IsNullOrEmpty(traceState))
            act.TraceStateString = traceState;
        act.Start();
        return act;
    }

    /// <summary>
    /// Restores the previous ambient activity when the workflow dispatch scope completes to prevent
    /// leaking trace context to the outer scope.
    /// </summary>
    internal readonly struct TraceActivityScope(Activity? activity, Activity? previous) : IDisposable
    {
        public void Dispose()
        {
            if (activity is not null)
            {
                Activity.Current = previous;
                activity.Dispose();
            }
        }
    }
}
