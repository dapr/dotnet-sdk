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

public class WorkflowActivityTests
{
    private sealed class LengthActivity : WorkflowActivity<string, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, string input)
            => Task.FromResult(input?.Length ?? 0);
    }

    private sealed class TestActivityContext : WorkflowActivityContext
    {
        public override TaskIdentifier Identifier => new("len");
        public override string InstanceId => "instance-1";
        public override string TaskExecutionKey => "exec-len-1";
    }

    [Fact]
    public async Task Generic_Base_Exposes_IWorkflowActivity_Types_And_Runs()
    {
        var activity = new LengthActivity();
        var i = (IWorkflowActivity)activity;

        Assert.Equal(typeof(string), i.InputType);
        Assert.Equal(typeof(int), i.OutputType);

        var ctx = new TestActivityContext();
        var result = await i.RunAsync(ctx, "abc");
        Assert.Equal(3, (int)result!);
    }
}
