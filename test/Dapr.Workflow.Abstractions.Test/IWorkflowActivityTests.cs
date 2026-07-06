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

public class IWorkflowActivityTests
{
    private sealed class EchoActivity : IWorkflowActivity
    {
        public Type InputType => typeof(string);
        public Type OutputType => typeof(string);

        public Task<object?> RunAsync(WorkflowActivityContext context, object? input)
        {
            // Prefix with instance id and identifier to ensure we used context
            var prefix = $"{context.InstanceId}:{context.Identifier.Name}:";
            return Task.FromResult<object?>(prefix + (input as string));
        }
    }

    private sealed class Ctx(string id, string instance) : WorkflowActivityContext
    {
        public override TaskIdentifier Identifier { get; } = new(id);
        public override string InstanceId { get; } = instance;
        public override string TaskExecutionKey { get; } = "exec-ctx-1";
    }

    [Fact]
    public async Task Activity_Reports_Types_And_Runs()
    {
        var act = new EchoActivity();
        Assert.Equal(typeof(string), act.InputType);
        Assert.Equal(typeof(string), act.OutputType);

        var ctx = new Ctx("echo", "wf-1");
        var result = await act.RunAsync(ctx, "hi");
        Assert.Equal("wf-1:echo:hi", result);
    }
}
