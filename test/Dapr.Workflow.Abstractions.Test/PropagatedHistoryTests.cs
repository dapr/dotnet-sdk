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

namespace Dapr.Workflow.Abstractions.Test;

public class PropagatedHistoryTests
{
    // ----- Helpers -----

    static PropagatedHistoryEvent MakeEvent(string instanceId = "id1", string appId = "app1", string name = "WF1") =>
        new(instanceId, appId, name, [], []);

    // ----- Events property -----

    [Fact]
    public void Events_ReturnsEmpty_WhenConstructedWithEmptyList()
    {
        var history = new PropagatedHistory([]);
        Assert.Empty(history.Events);
    }

    [Fact]
    public void Events_ReturnsProvidedEvents()
    {
        var e1 = MakeEvent("id1", "app1", "WF1");
        var e2 = MakeEvent("id2", "app2", "WF2");
        var history = new PropagatedHistory([e1, e2]);
        Assert.Equal(2, history.Events.Count);
        Assert.Same(e1, history.Events[0]);
        Assert.Same(e2, history.Events[1]);
    }

    // ----- GetAppIds -----

    [Fact]
    public void GetAppIds_ReturnsEmpty_WhenNoEvents()
    {
        var history = new PropagatedHistory([]);
        Assert.Empty(history.GetAppIds());
    }

    [Fact]
    public void GetAppIds_ReturnsOrderedDeduplicated_AppIds()
    {
        var events = new[]
        {
            MakeEvent("id1", "appA", "WF1"),
            MakeEvent("id2", "appB", "WF2"),
            MakeEvent("id3", "appA", "WF3"),
        };
        var history = new PropagatedHistory(events);
        var appIds = history.GetAppIds();
        Assert.Equal(2, appIds.Count);
        Assert.Equal("appA", appIds[0]);
        Assert.Equal("appB", appIds[1]);
    }

    [Fact]
    public void GetAppIds_IsCaseInsensitive_ForDeduplication()
    {
        var events = new[]
        {
            MakeEvent("id1", "AppA", "WF1"),
            MakeEvent("id2", "appa", "WF2"),
        };
        var history = new PropagatedHistory(events);
        var appIds = history.GetAppIds();
        Assert.Single(appIds);
        Assert.Equal("AppA", appIds[0]);
    }

    [Fact]
    public void GetAppIds_ReturnsAllDistinct_AppIds()
    {
        var events = new[]
        {
            MakeEvent("id1", "appX", "WF1"),
            MakeEvent("id2", "appY", "WF2"),
            MakeEvent("id3", "appZ", "WF3"),
        };
        var history = new PropagatedHistory(events);
        var appIds = history.GetAppIds();
        Assert.Equal(3, appIds.Count);
    }

    // ----- GetEventsByWorkflowName -----

    [Fact]
    public void GetEventsByWorkflowName_ThrowsOnNull()
    {
        var history = new PropagatedHistory([]);
        Assert.Throws<ArgumentNullException>(() => history.GetEventsByWorkflowName(null!));
    }

    [Fact]
    public void GetEventsByWorkflowName_ThrowsOnWhitespace()
    {
        var history = new PropagatedHistory([]);
        Assert.Throws<ArgumentException>(() => history.GetEventsByWorkflowName("   "));
    }

    [Fact]
    public void GetEventsByWorkflowName_ReturnsEmpty_WhenNoMatch()
    {
        var history = new PropagatedHistory([MakeEvent("id1", "app1", "WF1")]);
        Assert.Empty(history.GetEventsByWorkflowName("WF2"));
    }

    [Fact]
    public void GetEventsByWorkflowName_ReturnsMatching_InOrder()
    {
        var e1 = MakeEvent("id1", "app1", "WFMatch");
        var e2 = MakeEvent("id2", "app2", "WFOther");
        var e3 = MakeEvent("id3", "app3", "WFMatch");
        var history = new PropagatedHistory([e1, e2, e3]);

        var result = history.GetEventsByWorkflowName("WFMatch");
        Assert.Equal(2, result.Count);
        Assert.Same(e1, result[0]);
        Assert.Same(e3, result[1]);
    }

    [Fact]
    public void GetEventsByWorkflowName_IsCaseInsensitive()
    {
        var e = MakeEvent("id1", "app1", "MyWorkflow");
        var history = new PropagatedHistory([e]);
        Assert.Single(history.GetEventsByWorkflowName("myworkflow"));
        Assert.Single(history.GetEventsByWorkflowName("MYWORKFLOW"));
    }

    // ----- TryGetLastWorkflowEventByName -----

    [Fact]
    public void TryGetLastWorkflowEventByName_ThrowsOnNull()
    {
        var history = new PropagatedHistory([]);
        Assert.Throws<ArgumentNullException>(() => history.TryGetLastWorkflowEventByName(null!, out _));
    }

    [Fact]
    public void TryGetLastWorkflowEventByName_ThrowsOnWhitespace()
    {
        var history = new PropagatedHistory([]);
        Assert.Throws<ArgumentException>(() => history.TryGetLastWorkflowEventByName("  ", out _));
    }

    [Fact]
    public void TryGetLastWorkflowEventByName_ReturnsFalse_WhenNoMatch()
    {
        var history = new PropagatedHistory([MakeEvent("id1", "app1", "WF1")]);
        Assert.False(history.TryGetLastWorkflowEventByName("WF2", out var result));
        Assert.Null(result);
    }

    [Fact]
    public void TryGetLastWorkflowEventByName_ReturnsTrue_AndLastMatch()
    {
        var e1 = MakeEvent("id1", "app1", "WFRepeat");
        var e2 = MakeEvent("id2", "app2", "WFOther");
        var e3 = MakeEvent("id3", "app1", "WFRepeat");
        var history = new PropagatedHistory([e1, e2, e3]);

        Assert.True(history.TryGetLastWorkflowEventByName("WFRepeat", out var result));
        Assert.Same(e3, result);
    }

    [Fact]
    public void TryGetLastWorkflowEventByName_IsCaseInsensitive()
    {
        var e = MakeEvent("id1", "app1", "MyWorkflow");
        var history = new PropagatedHistory([e]);
        Assert.True(history.TryGetLastWorkflowEventByName("myworkflow", out var result));
        Assert.Same(e, result);
    }

    // ----- FilterByAppId -----

    [Fact]
    public void FilterByAppId_ThrowsOnNull()
    {
        var history = new PropagatedHistory([]);
        Assert.Throws<ArgumentNullException>(() => history.FilterByAppId(null!));
    }

    [Fact]
    public void FilterByAppId_ThrowsOnWhitespace()
    {
        var history = new PropagatedHistory([]);
        Assert.Throws<ArgumentException>(() => history.FilterByAppId(" "));
    }

    [Fact]
    public void FilterByAppId_ReturnsEmpty_WhenNoMatch()
    {
        var history = new PropagatedHistory([MakeEvent("id1", "appA", "WF1")]);
        Assert.Empty(history.FilterByAppId("appB"));
    }

    [Fact]
    public void FilterByAppId_ReturnsMatching_InOrder()
    {
        var e1 = MakeEvent("id1", "appA", "WF1");
        var e2 = MakeEvent("id2", "appB", "WF2");
        var e3 = MakeEvent("id3", "appA", "WF3");
        var history = new PropagatedHistory([e1, e2, e3]);

        var result = history.FilterByAppId("appA");
        Assert.Equal(2, result.Count);
        Assert.Same(e1, result[0]);
        Assert.Same(e3, result[1]);
    }

    [Fact]
    public void FilterByAppId_IsCaseInsensitive()
    {
        var e = MakeEvent("id1", "AppA", "WF1");
        var history = new PropagatedHistory([e]);
        Assert.Single(history.FilterByAppId("appa"));
        Assert.Single(history.FilterByAppId("APPA"));
    }

    // ----- FilterByInstanceId -----

    [Fact]
    public void FilterByInstanceId_ThrowsOnNull()
    {
        var history = new PropagatedHistory([]);
        Assert.Throws<ArgumentNullException>(() => history.FilterByInstanceId(null!));
    }

    [Fact]
    public void FilterByInstanceId_ThrowsOnWhitespace()
    {
        var history = new PropagatedHistory([]);
        Assert.Throws<ArgumentException>(() => history.FilterByInstanceId("  "));
    }

    [Fact]
    public void FilterByInstanceId_ReturnsEmpty_WhenNoMatch()
    {
        var history = new PropagatedHistory([MakeEvent("idA", "app1", "WF1")]);
        Assert.Empty(history.FilterByInstanceId("idB"));
    }

    [Fact]
    public void FilterByInstanceId_ReturnsMatching()
    {
        var e1 = MakeEvent("idA", "app1", "WF1");
        var e2 = MakeEvent("idB", "app2", "WF2");
        var e3 = MakeEvent("idA", "app3", "WF3");
        var history = new PropagatedHistory([e1, e2, e3]);

        var result = history.FilterByInstanceId("idA");
        Assert.Equal(2, result.Count);
        Assert.Same(e1, result[0]);
        Assert.Same(e3, result[1]);
    }

    [Fact]
    public void FilterByInstanceId_IsCaseSensitive()
    {
        var e = MakeEvent("idA", "app1", "WF1");
        var history = new PropagatedHistory([e]);
        Assert.Empty(history.FilterByInstanceId("ida"));
        Assert.Empty(history.FilterByInstanceId("IDA"));
        Assert.Single(history.FilterByInstanceId("idA"));
    }
}

public class PropagatedHistoryEventTests
{
    // ----- Helpers -----

    static PropagatedHistoryActivityResult MakeActivity(string name, PropagatedHistoryStatus status = PropagatedHistoryStatus.Completed) =>
        new(name, status, null, null, null);

    static PropagatedHistoryWorkflowResult MakeChildWorkflow(string name, PropagatedHistoryStatus status = PropagatedHistoryStatus.Completed) =>
        new(name, status, null, null);

    // ----- Constructor null guards -----

    [Fact]
    public void Constructor_ThrowsOnNull_InstanceId()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PropagatedHistoryEvent(null!, "appId", "name", [], []));
    }

    [Fact]
    public void Constructor_ThrowsOnNull_AppId()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PropagatedHistoryEvent("instanceId", null!, "name", [], []));
    }

    [Fact]
    public void Constructor_ThrowsOnNull_Name()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PropagatedHistoryEvent("instanceId", "appId", null!, [], []));
    }

    [Fact]
    public void Constructor_ThrowsOnNull_Activities()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PropagatedHistoryEvent("instanceId", "appId", "name", null!, []));
    }

    [Fact]
    public void Constructor_ThrowsOnNull_ChildWorkflows()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PropagatedHistoryEvent("instanceId", "appId", "name", [], null!));
    }

    [Fact]
    public void Constructor_SetsProperties()
    {
        var activities = new[] { MakeActivity("Act1") };
        var workflows = new[] { MakeChildWorkflow("CW1") };
        var evt = new PropagatedHistoryEvent("inst-1", "my-app", "MyWorkflow", activities, workflows);

        Assert.Equal("inst-1", evt.InstanceId);
        Assert.Equal("my-app", evt.AppId);
        Assert.Equal("MyWorkflow", evt.Name);
        Assert.Equal(activities, evt.Activities);
        Assert.Equal(workflows, evt.Workflows);
    }

    // ----- GetActivitiesByName -----

    [Fact]
    public void GetActivitiesByName_ThrowsOnNull()
    {
        var evt = new PropagatedHistoryEvent("id", "app", "wf", [], []);
        Assert.Throws<ArgumentNullException>(() => evt.GetActivitiesByName(null!));
    }

    [Fact]
    public void GetActivitiesByName_ThrowsOnWhitespace()
    {
        var evt = new PropagatedHistoryEvent("id", "app", "wf", [], []);
        Assert.Throws<ArgumentException>(() => evt.GetActivitiesByName("  "));
    }

    [Fact]
    public void GetActivitiesByName_ReturnsEmpty_WhenNoMatch()
    {
        var activities = new[] { MakeActivity("Act1") };
        var evt = new PropagatedHistoryEvent("id", "app", "wf", activities, []);
        Assert.Empty(evt.GetActivitiesByName("Act2"));
    }

    [Fact]
    public void GetActivitiesByName_ReturnsMatching_InOrder()
    {
        var a1 = MakeActivity("ActMatch");
        var a2 = MakeActivity("ActOther");
        var a3 = MakeActivity("ActMatch");
        var evt = new PropagatedHistoryEvent("id", "app", "wf", [a1, a2, a3], []);

        var result = evt.GetActivitiesByName("ActMatch");
        Assert.Equal(2, result.Count);
        Assert.Same(a1, result[0]);
        Assert.Same(a3, result[1]);
    }

    [Fact]
    public void GetActivitiesByName_IsCaseInsensitive()
    {
        var a = MakeActivity("MyActivity");
        var evt = new PropagatedHistoryEvent("id", "app", "wf", [a], []);
        Assert.Single(evt.GetActivitiesByName("myactivity"));
        Assert.Single(evt.GetActivitiesByName("MYACTIVITY"));
    }

    // ----- TryGetLastActivityByName -----

    [Fact]
    public void TryGetLastActivityByName_ThrowsOnNull()
    {
        var evt = new PropagatedHistoryEvent("id", "app", "wf", [], []);
        Assert.Throws<ArgumentNullException>(() => evt.TryGetLastActivityByName(null!, out _));
    }

    [Fact]
    public void TryGetLastActivityByName_ThrowsOnWhitespace()
    {
        var evt = new PropagatedHistoryEvent("id", "app", "wf", [], []);
        Assert.Throws<ArgumentException>(() => evt.TryGetLastActivityByName("  ", out _));
    }

    [Fact]
    public void TryGetLastActivityByName_ReturnsFalse_WhenNoMatch()
    {
        var evt = new PropagatedHistoryEvent("id", "app", "wf", [MakeActivity("Act1")], []);
        Assert.False(evt.TryGetLastActivityByName("Act2", out var result));
        Assert.Null(result);
    }

    [Fact]
    public void TryGetLastActivityByName_ReturnsTrue_AndLastMatch()
    {
        var a1 = MakeActivity("ActRepeat", PropagatedHistoryStatus.Completed);
        var a2 = MakeActivity("ActOther");
        var a3 = MakeActivity("ActRepeat", PropagatedHistoryStatus.Failed);
        var evt = new PropagatedHistoryEvent("id", "app", "wf", [a1, a2, a3], []);

        Assert.True(evt.TryGetLastActivityByName("ActRepeat", out var result));
        Assert.Same(a3, result);
    }

    [Fact]
    public void TryGetLastActivityByName_IsCaseInsensitive()
    {
        var a = MakeActivity("MyActivity");
        var evt = new PropagatedHistoryEvent("id", "app", "wf", [a], []);
        Assert.True(evt.TryGetLastActivityByName("myactivity", out var result));
        Assert.Same(a, result);
    }

    // ----- GetWorkflowsByName -----

    [Fact]
    public void GetWorkflowsByName_ThrowsOnNull()
    {
        var evt = new PropagatedHistoryEvent("id", "app", "wf", [], []);
        Assert.Throws<ArgumentNullException>(() => evt.GetWorkflowsByName(null!));
    }

    [Fact]
    public void GetWorkflowsByName_ThrowsOnWhitespace()
    {
        var evt = new PropagatedHistoryEvent("id", "app", "wf", [], []);
        Assert.Throws<ArgumentException>(() => evt.GetWorkflowsByName("  "));
    }

    [Fact]
    public void GetWorkflowsByName_ReturnsEmpty_WhenNoMatch()
    {
        var workflows = new[] { MakeChildWorkflow("CW1") };
        var evt = new PropagatedHistoryEvent("id", "app", "wf", [], workflows);
        Assert.Empty(evt.GetWorkflowsByName("CW2"));
    }

    [Fact]
    public void GetWorkflowsByName_ReturnsMatching_InOrder()
    {
        var cw1 = MakeChildWorkflow("CWMatch");
        var cw2 = MakeChildWorkflow("CWOther");
        var cw3 = MakeChildWorkflow("CWMatch");
        var evt = new PropagatedHistoryEvent("id", "app", "wf", [], [cw1, cw2, cw3]);

        var result = evt.GetWorkflowsByName("CWMatch");
        Assert.Equal(2, result.Count);
        Assert.Same(cw1, result[0]);
        Assert.Same(cw3, result[1]);
    }

    [Fact]
    public void GetWorkflowsByName_IsCaseInsensitive()
    {
        var cw = MakeChildWorkflow("MyChild");
        var evt = new PropagatedHistoryEvent("id", "app", "wf", [], [cw]);
        Assert.Single(evt.GetWorkflowsByName("mychild"));
        Assert.Single(evt.GetWorkflowsByName("MYCHILD"));
    }

    // ----- TryGetLastWorkflowByName -----

    [Fact]
    public void TryGetLastWorkflowByName_ThrowsOnNull()
    {
        var evt = new PropagatedHistoryEvent("id", "app", "wf", [], []);
        Assert.Throws<ArgumentNullException>(() => evt.TryGetLastWorkflowByName(null!, out _));
    }

    [Fact]
    public void TryGetLastWorkflowByName_ThrowsOnWhitespace()
    {
        var evt = new PropagatedHistoryEvent("id", "app", "wf", [], []);
        Assert.Throws<ArgumentException>(() => evt.TryGetLastWorkflowByName("  ", out _));
    }

    [Fact]
    public void TryGetLastWorkflowByName_ReturnsFalse_WhenNoMatch()
    {
        var evt = new PropagatedHistoryEvent("id", "app", "wf", [], [MakeChildWorkflow("CW1")]);
        Assert.False(evt.TryGetLastWorkflowByName("CW2", out var result));
        Assert.Null(result);
    }

    [Fact]
    public void TryGetLastWorkflowByName_ReturnsTrue_AndLastMatch()
    {
        var cw1 = MakeChildWorkflow("CWRepeat", PropagatedHistoryStatus.Completed);
        var cw2 = MakeChildWorkflow("CWOther");
        var cw3 = MakeChildWorkflow("CWRepeat", PropagatedHistoryStatus.Failed);
        var evt = new PropagatedHistoryEvent("id", "app", "wf", [], [cw1, cw2, cw3]);

        Assert.True(evt.TryGetLastWorkflowByName("CWRepeat", out var result));
        Assert.Same(cw3, result);
    }

    [Fact]
    public void TryGetLastWorkflowByName_IsCaseInsensitive()
    {
        var cw = MakeChildWorkflow("MyChild");
        var evt = new PropagatedHistoryEvent("id", "app", "wf", [], [cw]);
        Assert.True(evt.TryGetLastWorkflowByName("mychild", out var result));
        Assert.Same(cw, result);
    }
}

public class PropagatedHistoryActivityResultTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var failure = new WorkflowTaskFailureDetails(typeof(InvalidOperationException).FullName!, "err");
        var result = new PropagatedHistoryActivityResult("MyAct", PropagatedHistoryStatus.Failed, """{"x":1}""", null, failure);

        Assert.Equal("MyAct", result.Name);
        Assert.Equal(PropagatedHistoryStatus.Failed, result.Status);
        Assert.Equal("""{"x":1}""", result.Input);
        Assert.Null(result.Output);
        Assert.Same(failure, result.FailureDetails);
    }

    [Fact]
    public void Constructor_SetsNullableFields_ToNull()
    {
        var result = new PropagatedHistoryActivityResult("Act1", PropagatedHistoryStatus.Pending, null, null, null);
        Assert.Null(result.Input);
        Assert.Null(result.Output);
        Assert.Null(result.FailureDetails);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var r1 = new PropagatedHistoryActivityResult("Act", PropagatedHistoryStatus.Completed, "in", "out", null);
        var r2 = new PropagatedHistoryActivityResult("Act", PropagatedHistoryStatus.Completed, "in", "out", null);
        Assert.Equal(r1, r2);
    }

    [Fact]
    public void RecordEquality_DifferentName_AreNotEqual()
    {
        var r1 = new PropagatedHistoryActivityResult("Act1", PropagatedHistoryStatus.Completed, null, null, null);
        var r2 = new PropagatedHistoryActivityResult("Act2", PropagatedHistoryStatus.Completed, null, null, null);
        Assert.NotEqual(r1, r2);
    }

    [Fact]
    public void RecordEquality_DifferentStatus_AreNotEqual()
    {
        var r1 = new PropagatedHistoryActivityResult("Act", PropagatedHistoryStatus.Completed, null, null, null);
        var r2 = new PropagatedHistoryActivityResult("Act", PropagatedHistoryStatus.Failed, null, null, null);
        Assert.NotEqual(r1, r2);
    }

    [Fact]
    public void CompletedActivity_HasOutputAndNoFailureDetails()
    {
        var result = new PropagatedHistoryActivityResult("Act", PropagatedHistoryStatus.Completed, """{"in":true}""", """{"out":42}""", null);
        Assert.Equal(PropagatedHistoryStatus.Completed, result.Status);
        Assert.NotNull(result.Output);
        Assert.Null(result.FailureDetails);
    }
}

public class PropagatedHistoryWorkflowResultTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var failure = new WorkflowTaskFailureDetails(typeof(Exception).FullName!, "err");
        var result = new PropagatedHistoryWorkflowResult("ChildWF", PropagatedHistoryStatus.Failed, null, failure);

        Assert.Equal("ChildWF", result.Name);
        Assert.Equal(PropagatedHistoryStatus.Failed, result.Status);
        Assert.Null(result.Output);
        Assert.Same(failure, result.FailureDetails);
    }

    [Fact]
    public void Constructor_SetsNullableFields_ToNull()
    {
        var result = new PropagatedHistoryWorkflowResult("CW", PropagatedHistoryStatus.Pending, null, null);
        Assert.Null(result.Output);
        Assert.Null(result.FailureDetails);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var r1 = new PropagatedHistoryWorkflowResult("CW", PropagatedHistoryStatus.Completed, "out", null);
        var r2 = new PropagatedHistoryWorkflowResult("CW", PropagatedHistoryStatus.Completed, "out", null);
        Assert.Equal(r1, r2);
    }

    [Fact]
    public void RecordEquality_DifferentName_AreNotEqual()
    {
        var r1 = new PropagatedHistoryWorkflowResult("CW1", PropagatedHistoryStatus.Completed, null, null);
        var r2 = new PropagatedHistoryWorkflowResult("CW2", PropagatedHistoryStatus.Completed, null, null);
        Assert.NotEqual(r1, r2);
    }

    [Fact]
    public void RecordEquality_DifferentStatus_AreNotEqual()
    {
        var r1 = new PropagatedHistoryWorkflowResult("CW", PropagatedHistoryStatus.Completed, null, null);
        var r2 = new PropagatedHistoryWorkflowResult("CW", PropagatedHistoryStatus.Pending, null, null);
        Assert.NotEqual(r1, r2);
    }

    [Fact]
    public void CompletedWorkflow_HasOutputAndNoFailureDetails()
    {
        var result = new PropagatedHistoryWorkflowResult("CW", PropagatedHistoryStatus.Completed, """{"done":true}""", null);
        Assert.Equal(PropagatedHistoryStatus.Completed, result.Status);
        Assert.NotNull(result.Output);
        Assert.Null(result.FailureDetails);
    }
}

public class PropagatedHistoryStatusTests
{
    [Fact]
    public void Enum_HasExpectedValues()
    {
        Assert.Equal(0, (int)PropagatedHistoryStatus.Pending);
        Assert.Equal(1, (int)PropagatedHistoryStatus.Completed);
        Assert.Equal(2, (int)PropagatedHistoryStatus.Failed);
    }

    [Fact]
    public void Enum_HasExactlyThreeValues()
    {
        var values = Enum.GetValues<PropagatedHistoryStatus>();
        Assert.Equal(3, values.Length);
    }

    [Theory]
    [InlineData(PropagatedHistoryStatus.Pending)]
    [InlineData(PropagatedHistoryStatus.Completed)]
    [InlineData(PropagatedHistoryStatus.Failed)]
    public void Status_IsStoredCorrectly_InActivityResult(PropagatedHistoryStatus status)
    {
        var result = new PropagatedHistoryActivityResult("Act", status, null, null, null);
        Assert.Equal(status, result.Status);
    }

    [Theory]
    [InlineData(PropagatedHistoryStatus.Pending)]
    [InlineData(PropagatedHistoryStatus.Completed)]
    [InlineData(PropagatedHistoryStatus.Failed)]
    public void Status_IsStoredCorrectly_InWorkflowResult(PropagatedHistoryStatus status)
    {
        var result = new PropagatedHistoryWorkflowResult("CW", status, null, null);
        Assert.Equal(status, result.Status);
    }
}
