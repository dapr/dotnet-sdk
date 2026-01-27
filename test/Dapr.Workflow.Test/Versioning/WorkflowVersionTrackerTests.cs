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
    public void Constructor_ExtractsPatchesFromHistory_PreservingOrder()
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
    public void Constructor_ExtractsPatchesFromHistory_AllowsDuplicates()
    {
        // Arrange
        var history = new List<HistoryEvent>
        {
            new() { OrchestratorStarted = new() { Version = new() { Patches = { "p1", "p1", "p2" } } } }
        };

        // Act
        var tracker = new WorkflowVersionTracker(history);

        // Assert
        tracker.AggregatedPatchesOrdered.ShouldBe(["p1", "p1", "p2"]);
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
    public void RequestPatch_NonReplay_AllowsDuplicateEvaluations_AndStampsDuplicates()
    {
        var tracker = new WorkflowVersionTracker([]);

        tracker.RequestPatch("p1", isReplaying: false).ShouldBeTrue();
        tracker.RequestPatch("p1", isReplaying: false).ShouldBeTrue();

        var version = tracker.BuildResponseVersion("wf");
        version.Patches.ShouldBe(["p1", "p1"]);
        tracker.IsStalled.ShouldBeFalse();
    }

    [Fact]
    public void RequestPatch_Replay_ValidatesOrderAgainstHistory()
    {
        var history = new List<HistoryEvent>
        {
            new() { OrchestratorStarted = new() { Version = new() { Patches = { "p1", "p2" } } } }
        };
        var tracker = new WorkflowVersionTracker(history);

        tracker.RequestPatch("p1", isReplaying: true).ShouldBeTrue();
        tracker.RequestPatch("p2", isReplaying: true).ShouldBeTrue();
        tracker.IsStalled.ShouldBeFalse();
        tracker.IncludeVersionInNextResponse.ShouldBeFalse(); // Replay shouldn't trigger response stamp
    }

    [Fact]
    public void RequestPatch_Replay_OrderMismatch_Stalls()
    {
        var history = new List<HistoryEvent>
        {
            new() { OrchestratorStarted = new() { Version = new() { Patches = { "p1", "p2" } } } }
        };
        var tracker = new WorkflowVersionTracker(history);

        tracker.RequestPatch("p2", isReplaying: true).ShouldBeFalse();

        tracker.IsStalled.ShouldBeTrue();
        tracker.StalledEvent.ShouldNotBeNull();
        tracker.StalledEvent.Reason.ShouldBe(StalledReason.PatchMismatch);
        tracker.StalledEvent.Description.ShouldContain("Expected 'p1'");
    }

    [Fact]
    public void ValidateReplayConsumedHistoryPatches_WhenHistoryHasMoreThanReplayed_Stalls()
    {
        var history = new List<HistoryEvent>
        {
            new() { OrchestratorStarted = new() { Version = new() { Patches = { "p1", "p2" } } } }
        };
        var tracker = new WorkflowVersionTracker(history);

        tracker.RequestPatch("p1", isReplaying: true).ShouldBeTrue();

        tracker.ValidateReplayConsumedHistoryPatches();

        tracker.IsStalled.ShouldBeTrue();
        tracker.StalledEvent.ShouldNotBeNull();
        tracker.StalledEvent.Reason.ShouldBe(StalledReason.PatchMismatch);
        tracker.StalledEvent.Description.ShouldContain("Replay did not evaluate all historical patches");
    }
    
    [Fact]
    public void OnOrchestratorStarted_WithNullVersion_DoesNothing()
    {
        var tracker = new WorkflowVersionTracker([]);

        tracker.OnOrchestratorStarted(null);

        tracker.IsStalled.ShouldBeFalse();
        tracker.AggregatedPatchesOrdered.ShouldBeEmpty();
    }

    [Fact]
    public void RequestPatch_WithInvalidName_ThrowsArgumentException()
    {
        var tracker = new WorkflowVersionTracker([]);

        Assert.Throws<ArgumentException>(() => tracker.RequestPatch("", isReplaying: false));
        Assert.Throws<ArgumentException>(() => tracker.RequestPatch("   ", isReplaying: false));
    }

    [Fact]
    public void RequestPatch_WhenAlreadyStalled_ReturnsFalse()
    {
        var history = new List<HistoryEvent>
        {
            new() { OrchestratorStarted = new() { Version = new() { Patches = { "p1" } } } }
        };
        var tracker = new WorkflowVersionTracker(history);

        // Force stall by mismatching replay order
        tracker.RequestPatch("not-p1", isReplaying: true).ShouldBeFalse();
        tracker.IsStalled.ShouldBeTrue();

        tracker.RequestPatch("p2", isReplaying: false).ShouldBeFalse();
        tracker.RequestPatch("p2", isReplaying: true).ShouldBeFalse();
    }

    [Fact]
    public void BuildResponseVersion_ReturnsCurrentTurnExecutionPath()
    {
        var tracker = new WorkflowVersionTracker([]);
        tracker.RequestPatch("p1", isReplaying: false);
        tracker.RequestPatch("p2", isReplaying: false);

        var version = tracker.BuildResponseVersion("MyWorkflow");

        version.Name.ShouldBe("MyWorkflow");
        version.Patches.ShouldBe(["p1", "p2"]);
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
