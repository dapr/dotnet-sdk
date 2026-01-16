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

using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Versioning;
using Shouldly;

namespace Dapr.Workflow.Test.Versioning;

public sealed class WorkflowVersionTrackerTests
{
    [Fact]
    public void Constructor_ExtractsPatchesFromHistory()
    {
        // Arrange
        var history = new List<HistoryEvent>
        {
            new() { OrchestratorStarted = new() { Version = new() { Patches = { "patch1" } } } },
            new() { OrchestratorStarted = new() { Version = new() { Patches = { "patch2" } } } }
        };

        // Act
        var tracker = new WorkflowVersionTracker(history);

        // Assert
        tracker.AggregatedPatchesOrdered.ShouldBe(["patch1", "patch2"]);
    }

    [Fact]
    public void RequestPatch_NonReplay_ReturnsTrueAndSetsFlag()
    {
        var tracker = new WorkflowVersionTracker([]);

        var result = tracker.RequestPatch("new-patch", isReplaying: false);

        result.ShouldBeTrue();
        tracker.IncludeVersionInNextResponse.ShouldBeTrue();
        tracker.IsStalled.ShouldBeFalse();
    }

    [Fact]
    public void RequestPatch_Replay_ReturnsTrueOnlyIfInHistory()
    {
        var history = new List<HistoryEvent>
        {
            new() { OrchestratorStarted = new() { Version = new() { Patches = { "patch1" } } } }
        };
        var tracker = new WorkflowVersionTracker(history);

        tracker.RequestPatch("patch1", isReplaying: true).ShouldBeTrue();
        tracker.RequestPatch("patch2", isReplaying: true).ShouldBeFalse();
        tracker.IncludeVersionInNextResponse.ShouldBeFalse(); // Replay shouldn't trigger response stamp
    }

    [Fact]
    public void RequestPatch_DuplicateName_Stalls()
    {
        var tracker = new WorkflowVersionTracker([]);
        tracker.RequestPatch("unique-patch", isReplaying: false);

        var result = tracker.RequestPatch("unique-patch", isReplaying: false);

        result.ShouldBeFalse();
        tracker.IsStalled.ShouldBeTrue();
        tracker.StalledEvent.ShouldNotBeNull();
        tracker.StalledEvent.Reason.ShouldBe(StalledReason.PatchMismatch);
        tracker.StalledEvent.Description.ShouldContain("Duplicate patch");
    }

    [Fact]
    public void OnOrchestratorStarted_NewPatchInHistoryNotReachedByCode_Stalls()
    {
        var tracker = new WorkflowVersionTracker([]);
        var runtimeVersion = new OrchestrationVersion { Patches = { "patch-from-future" } };

        tracker.OnOrchestratorStarted(runtimeVersion);

        tracker.IsStalled.ShouldBeTrue();
        tracker.StalledEvent.ShouldNotBeNull();
        tracker.StalledEvent.Reason.ShouldBe(StalledReason.PatchMismatch);
        tracker.StalledEvent.Description.ShouldContain("not yet enabled by current code path");
    }

    [Fact]
    public void OnOrchestratorStarted_PatchAlreadyReachedByCode_Proceeds()
    {
        var tracker = new WorkflowVersionTracker([]);
        tracker.RequestPatch("expected-patch", isReplaying: false);
        var runtimeVersion = new OrchestrationVersion { Patches = { "expected-patch" } };

        tracker.OnOrchestratorStarted(runtimeVersion);

        tracker.IsStalled.ShouldBeFalse();
        tracker.AggregatedPatchesOrdered.ShouldContain("expected-patch");
    }

    [Fact]
    public void BuildResponseVersion_ReturnsCurrentExecutionPath()
    {
        var tracker = new WorkflowVersionTracker([]);
        tracker.RequestPatch("p1", isReplaying: false);
        tracker.RequestPatch("p2", isReplaying: false);

        var version = tracker.BuildResponseVersion("MyWorkflow");

        version.Name.ShouldBe("MyWorkflow");
        version.Patches.ShouldBe(["p1", "p2"]);
    }
}
