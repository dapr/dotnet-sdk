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

public class WorkflowActivityContextTests
{
    private sealed class TestContext : WorkflowActivityContext
    {
        private readonly TaskIdentifier _id;
        private readonly string _instanceId;
        private readonly string _taskExecutionKey;

        public TestContext(TaskIdentifier id, string instanceId, string taskExecutionKey = "exec-1")
        {
            _id = id;
            _instanceId = instanceId;
            _taskExecutionKey = taskExecutionKey;
        }

        public override TaskIdentifier Identifier => _id;
        public override string InstanceId => _instanceId;
        public override string TaskExecutionKey => _taskExecutionKey;
    }

    [Fact]
    public void Properties_Return_Constructor_Values()
    {
        var ctx = new TestContext(new TaskIdentifier("act-1"), "wf-123", "run-42");
        Assert.Equal("act-1", ctx.Identifier.Name);
        Assert.Equal("wf-123", ctx.InstanceId);
        Assert.Equal("run-42", ctx.TaskExecutionKey);
    }
}
