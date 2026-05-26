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

using System.Diagnostics;
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Worker;

namespace Dapr.Workflow.Test.Worker;

public class WorkflowTraceTests
{
    [Fact]
    public void StartActivityTrace_ShouldEmitActivitySourceSpan_WhenListenerIsRegistered()
    {
        using var listener = new ActivityListener();
        var startedCount = 0;
        listener.ShouldListenTo = src => src.Name == "Dapr.Workflow";
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.ActivityStarted = _ => startedCount++;
        ActivitySource.AddActivityListener(listener);

        const string traceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
        var request = new ActivityRequest
        {
            Name = "act",
            ParentTraceContext = new TraceContext { TraceParent = traceParent }
        };

        Activity.Current = null;

        using var scope = WorkflowTrace.StartActivityTrace(request);

        Assert.NotNull(Activity.Current);
        Assert.Equal("Dapr.Workflow", Activity.Current.Source.Name);
        Assert.Equal(1, startedCount);
    }

    [Fact]
    public void StartOrchestrationTrace_ShouldRestoreAmbientTraceContext_WithoutEmittingActivitySourceSpan()
    {
        using var listener = new ActivityListener();
        var startedCount = 0;
        listener.ShouldListenTo = src => src.Name == "Dapr.Workflow";
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.ActivityStarted = _ => startedCount++;
        ActivitySource.AddActivityListener(listener);

        const string expectedTraceId = "0af7651916cd43dd8448eb211c80319c";
        const string traceParent = $"00-{expectedTraceId}-b7ad6b7169203331-01";
        var events = new[]
        {
            new HistoryEvent
            {
                ExecutionStarted = new ExecutionStartedEvent
                {
                    ParentTraceContext = new TraceContext { TraceParent = traceParent }
                }
            }
        };

        Activity.Current = null;

        using var scope = WorkflowTrace.StartOrchestrationTrace(events);

        Assert.NotNull(Activity.Current);
        Assert.Equal(expectedTraceId, Activity.Current.TraceId.ToHexString());
        Assert.Equal(string.Empty, Activity.Current.Source.Name);
        Assert.Equal(0, startedCount);
    }
}
