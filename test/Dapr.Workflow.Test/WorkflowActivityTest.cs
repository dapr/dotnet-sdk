// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

namespace Dapr.Workflow.Test;

using Moq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

/// <summary>
/// Contains tests for WorkflowActivityContext.
/// </summary>
public class WorkflowActivityTest
{
    private IWorkflowActivity workflowActivity;

    private Mock<WorkflowActivityContext> workflowActivityContextMock;

    public WorkflowActivityTest()
    {
        this.workflowActivity = new TestDaprWorkflowActivity();
        this.workflowActivityContextMock = new Mock<WorkflowActivityContext>();
    }

    [Fact]
    public async Task RunAsync_ShouldReturnCorrectContextInstanceId()
    {
        this.workflowActivityContextMock.Setup((x) => x.InstanceId).Returns("instanceId");

        string result = (string) (await this.workflowActivity.RunAsync(this.workflowActivityContextMock.Object, "input"))!;

        Assert.Equal("instanceId", result);
    }


    public class TestDaprWorkflowActivity : WorkflowActivity<string, string>
    {
        public override Task<string> RunAsync(WorkflowActivityContext context, string input)
        {
            return Task.FromResult(context.InstanceId);
        }
    }
}