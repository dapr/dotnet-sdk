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

namespace Dapr.Workflow.Test;

public class WorkflowRuntimeOptionsTests
{
    [Fact]
    public void RegisterWorkflow_WithoutName_AddsWorkflowWithTypeName()
    {
        var options = new WorkflowRuntimeOptions();
        options.RegisterWorkflow<TestWorkflow>();
        Assert.Contains(typeof(TestWorkflow).Name, options.FactoriesInternal);
    }

    [Fact]
    public void RegisterWorkflow_WithName_AddsWorkflowWithSpecifiedName()
    {
        var options = new WorkflowRuntimeOptions();
        options.RegisterWorkflow<TestWorkflow>("MyWorkflow_v1.0");
        Assert.Contains("MyWorkflow_v1.0", options.FactoriesInternal);
    }

    [Fact]
    public void RegisterActivity_WithoutName_AddsWorkflowActivityWithTypeName()
    {
        var options = new WorkflowRuntimeOptions();
        options.RegisterActivity<TestWorkflowActivity>();
        Assert.Contains(typeof(TestWorkflowActivity).Name, options.FactoriesInternal);
    }

    [Fact]
    public void RegisterActivity_WithName_AddsWorkflowActivityWithSpecifiedName()
    {
        var options = new WorkflowRuntimeOptions();
        options.RegisterActivity<TestWorkflowActivity>("MyActivity_v1.0");
        Assert.Contains("MyActivity_v1.0", options.FactoriesInternal);
    }

    public class TestWorkflow : Workflow<string, string>
    {
        public override Task<string> RunAsync(WorkflowContext context, string input)
        {
            return Task.FromResult(input);
        }
    }

    public class TestWorkflowActivity : WorkflowActivity<string, string>
    {
        public override Task<string> RunAsync(WorkflowActivityContext context, string input)
        {
            return Task.FromResult(input);
        }
    }
}
