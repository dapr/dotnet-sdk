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

public class WorkflowRuntimeStatusTests
{
    [Fact]
    public void Enum_Has_Expected_Values()
    {
        Assert.Equal(-1, (int)WorkflowRuntimeStatus.Unknown);
        Assert.Equal(0, (int)WorkflowRuntimeStatus.Running);
        Assert.Equal(1, (int)WorkflowRuntimeStatus.Completed);
        Assert.Equal(2, (int)WorkflowRuntimeStatus.ContinuedAsNew);
        Assert.Equal(3, (int)WorkflowRuntimeStatus.Failed);
        Assert.Equal(4, (int)WorkflowRuntimeStatus.Canceled);
        Assert.Equal(5, (int)WorkflowRuntimeStatus.Terminated);
        Assert.Equal(6, (int)WorkflowRuntimeStatus.Pending);
        Assert.Equal(7, (int)WorkflowRuntimeStatus.Suspended);
        Assert.Equal(8, (int)WorkflowRuntimeStatus.Stalled);
    }

    [Fact]
    public void Enum_ToString_Returns_Names()
    {
        Assert.Equal("Unknown", WorkflowRuntimeStatus.Unknown.ToString());
        Assert.Equal("Running", WorkflowRuntimeStatus.Running.ToString());
        Assert.Equal("Completed", WorkflowRuntimeStatus.Completed.ToString());
        Assert.Equal("ContinuedAsNew", WorkflowRuntimeStatus.ContinuedAsNew.ToString());
        Assert.Equal("Failed", WorkflowRuntimeStatus.Failed.ToString());
        Assert.Equal("Canceled", WorkflowRuntimeStatus.Canceled.ToString());
        Assert.Equal("Terminated", WorkflowRuntimeStatus.Terminated.ToString());
        Assert.Equal("Pending", WorkflowRuntimeStatus.Pending.ToString());
        Assert.Equal("Suspended", WorkflowRuntimeStatus.Suspended.ToString());
        Assert.Equal("Stalled", WorkflowRuntimeStatus.Stalled.ToString());
    }
}
