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

using Dapr.Workflow.Abstractions;
using Dapr.Workflow.Worker;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dapr.Workflow.Test;

public class WorkflowRuntimeOptionsTests
{
    [Fact]
    public void UseGrpcChannelOptions_ShouldThrowArgumentNullException_WhenNull()
    {
        var options = new WorkflowRuntimeOptions();

        Assert.Throws<ArgumentNullException>(() => options.UseGrpcChannelOptions(null!));
    }

    [Fact]
    public void ApplyRegistrations_ShouldThrowArgumentNullException_WhenFactoryIsNull()
    {
        var options = new WorkflowRuntimeOptions();

        Assert.Throws<ArgumentNullException>(() => options.ApplyRegistrations(null!));
    }

    [Fact]
    public void ApplyRegistrations_ShouldApplyWorkflowAndActivityRegistrations_ToFactory()
    {
        var options = new WorkflowRuntimeOptions();

        options.RegisterWorkflow<int, int>("wf-fn", (_, x) => Task.FromResult(x + 1));
        options.RegisterActivity<int, int>("act-fn", (_, x) => Task.FromResult(x + 2));

        var factory = new WorkflowsFactory(NullLogger<WorkflowsFactory>.Instance);
        options.ApplyRegistrations(factory);

        var sp = new ServiceCollection().BuildServiceProvider();

        Assert.True(factory.TryCreateWorkflow(new("wf-fn"), sp, out var workflow, out _));
        Assert.NotNull(workflow);

        Assert.True(factory.TryCreateActivity(new("act-fn"), sp, out var activity, out _));
        Assert.NotNull(activity);
    }

    [Fact]
    public void RegisterWorkflow_GenericWithName_ShouldRegisterUsingProvidedName_WhenApplied()
    {
        var options = new WorkflowRuntimeOptions();
        options.RegisterWorkflow<TestWorkflow>(name: "MyCustomWorkflowName");

        var factory = new WorkflowsFactory(NullLogger<WorkflowsFactory>.Instance);
        options.ApplyRegistrations(factory);

        var sp = new ServiceCollection().BuildServiceProvider();

        Assert.True(factory.TryCreateWorkflow(new TaskIdentifier("MyCustomWorkflowName"), sp, out var workflow, out _));
        Assert.NotNull(workflow);
        Assert.IsType<TestWorkflow>(workflow);

        Assert.False(factory.TryCreateWorkflow(new TaskIdentifier(nameof(TestWorkflow)), sp, out _, out _));
    }

    [Fact]
    public void RegisterActivity_GenericWithName_ShouldRegisterUsingProvidedName_WhenApplied()
    {
        var options = new WorkflowRuntimeOptions();
        options.RegisterActivity<TestActivity>(name: "MyCustomActivityName");

        var factory = new WorkflowsFactory(NullLogger<WorkflowsFactory>.Instance);
        options.ApplyRegistrations(factory);

        var sp = new ServiceCollection().BuildServiceProvider();

        Assert.True(factory.TryCreateActivity(new TaskIdentifier("MyCustomActivityName"), sp, out var activity, out _));
        Assert.NotNull(activity);
        Assert.IsType<TestActivity>(activity);

        Assert.False(factory.TryCreateActivity(new TaskIdentifier(nameof(TestActivity)), sp, out _, out _));
    }

    private sealed class TestWorkflow : IWorkflow
    {
        public Type InputType => typeof(object);
        public Type OutputType => typeof(object);

        public Task<object?> RunAsync(WorkflowContext context, object? input) => Task.FromResult<object?>(null);
    }

    private sealed class TestActivity : IWorkflowActivity
    {
        public Type InputType => typeof(object);
        public Type OutputType => typeof(object);

        public Task<object?> RunAsync(WorkflowActivityContext context, object? input) => Task.FromResult<object?>(null);
    }
}
