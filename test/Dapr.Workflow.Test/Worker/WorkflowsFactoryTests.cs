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
//  ------------------------------------------------------------------------

using Dapr.Workflow.Abstractions;
using Dapr.Workflow.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Dapr.Workflow.Test.Worker;

public class WorkflowsFactoryTests
{
    [Fact]
    public void RegisterWorkflow_Generic_ShouldDefaultNameToTypeName_AndCreateViaDI()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Dependency("dep-1"));
        var sp = services.BuildServiceProvider();

        var logger = Mock.Of<ILogger<WorkflowsFactory>>();
        var factory = new WorkflowsFactory(logger);

        factory.RegisterWorkflow<TestWorkflowWithDependency>();

        var created = factory.TryCreateWorkflow(new TaskIdentifier(nameof(TestWorkflowWithDependency)), sp, out var workflow);

        Assert.True(created);
        Assert.NotNull(workflow);
        Assert.IsType<TestWorkflowWithDependency>(workflow);
        Assert.Equal("dep-1", ((TestWorkflowWithDependency)workflow!).Dep.Value);
    }

    [Fact]
    public void RegisterActivity_Generic_ShouldDefaultNameToTypeName_AndCreateViaDI()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Dependency("dep-2"));
        var sp = services.BuildServiceProvider();

        var logger = Mock.Of<ILogger<WorkflowsFactory>>();
        var factory = new WorkflowsFactory(logger);

        factory.RegisterActivity<TestActivityWithDependency>();

        var created = factory.TryCreateActivity(new TaskIdentifier(nameof(TestActivityWithDependency)), sp, out var activity);

        Assert.True(created);
        Assert.NotNull(activity);
        Assert.IsType<TestActivityWithDependency>(activity);
        Assert.Equal("dep-2", ((TestActivityWithDependency)activity!).Dep.Value);
    }

    [Fact]
    public void RegisterWorkflow_Function_ShouldThrowArgumentException_WhenNameIsNullOrWhitespace()
    {
        var logger = Mock.Of<ILogger<WorkflowsFactory>>();
        var factory = new WorkflowsFactory(logger);

        Assert.Throws<ArgumentException>(() => factory.RegisterWorkflow<int, int>("", (_, x) => Task.FromResult(x)));
        Assert.Throws<ArgumentException>(() => factory.RegisterWorkflow<int, int>("  ", (_, x) => Task.FromResult(x)));
    }

    [Fact]
    public void RegisterWorkflow_Function_ShouldThrowArgumentNullException_WhenImplementationIsNull()
    {
        var logger = Mock.Of<ILogger<WorkflowsFactory>>();
        var factory = new WorkflowsFactory(logger);

        Assert.Throws<ArgumentNullException>(() => factory.RegisterWorkflow<int, int>("wf", null!));
    }

    [Fact]
    public void RegisterActivity_Function_ShouldThrowArgumentException_WhenNameIsNullOrWhitespace()
    {
        var logger = Mock.Of<ILogger<WorkflowsFactory>>();
        var factory = new WorkflowsFactory(logger);

        Assert.Throws<ArgumentException>(() => factory.RegisterActivity<int, int>("", (_, x) => Task.FromResult(x)));
        Assert.Throws<ArgumentException>(() => factory.RegisterActivity<int, int>("  ", (_, x) => Task.FromResult(x)));
    }

    [Fact]
    public void RegisterActivity_Function_ShouldThrowArgumentNullException_WhenImplementationIsNull()
    {
        var logger = Mock.Of<ILogger<WorkflowsFactory>>();
        var factory = new WorkflowsFactory(logger);

        Assert.Throws<ArgumentNullException>(() => factory.RegisterActivity<int, int>("act", null!));
    }

    [Fact]
    public void TryCreateWorkflow_ShouldReturnFalse_WhenNotRegistered()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var logger = Mock.Of<ILogger<WorkflowsFactory>>();
        var factory = new WorkflowsFactory(logger);

        var created = factory.TryCreateWorkflow(new TaskIdentifier("missing"), sp, out var workflow);

        Assert.False(created);
        Assert.Null(workflow);
    }

    [Fact]
    public void TryCreateActivity_ShouldReturnFalse_WhenNotRegistered()
    {
        var sp = new ServiceCollection().BuildServiceProvider();
        var logger = Mock.Of<ILogger<WorkflowsFactory>>();
        var factory = new WorkflowsFactory(logger);

        var created = factory.TryCreateActivity(new TaskIdentifier("missing"), sp, out var activity);

        Assert.False(created);
        Assert.Null(activity);
    }

    [Fact]
    public void TryCreateWorkflow_ShouldReturnFalse_WhenFactoryThrows()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();

        var logger = Mock.Of<ILogger<WorkflowsFactory>>();
        var factory = new WorkflowsFactory(logger);

        factory.RegisterWorkflow<ThrowingWorkflow>();

        var created = factory.TryCreateWorkflow(new TaskIdentifier(nameof(ThrowingWorkflow)), sp, out var workflow);

        Assert.False(created);
        Assert.Null(workflow);
    }

    [Fact]
    public void TryCreateActivity_ShouldReturnFalse_WhenFactoryThrows()
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();

        var logger = Mock.Of<ILogger<WorkflowsFactory>>();
        var factory = new WorkflowsFactory(logger);

        factory.RegisterActivity<ThrowingActivity>();

        var created = factory.TryCreateActivity(new TaskIdentifier(nameof(ThrowingActivity)), sp, out var activity);

        Assert.False(created);
        Assert.Null(activity);
    }

    [Fact]
    public async Task RegisteredFunctionWorkflow_ShouldInvokeImplementation_AndUseTypedInput()
    {
        var logger = Mock.Of<ILogger<WorkflowsFactory>>();
        var factory = new WorkflowsFactory(logger);

        factory.RegisterWorkflow<int, string>("wf-fn", (_, x) => Task.FromResult($"v:{x}"));

        var sp = new ServiceCollection().BuildServiceProvider();
        var created = factory.TryCreateWorkflow(new TaskIdentifier("wf-fn"), sp, out var workflow);

        Assert.True(created);
        Assert.NotNull(workflow);
        Assert.Equal(typeof(int), workflow!.InputType);
        Assert.Equal(typeof(string), workflow.OutputType);

        var result = await workflow.RunAsync(new FakeWorkflowContext(), 7);

        Assert.Equal("v:7", result);
    }

    [Fact]
    public async Task RegisteredFunctionActivity_ShouldInvokeImplementation_AndUseTypedInput()
    {
        var logger = Mock.Of<ILogger<WorkflowsFactory>>();
        var factory = new WorkflowsFactory(logger);

        factory.RegisterActivity<int, string>("act-fn", (_, x) => Task.FromResult($"v:{x}"));

        var sp = new ServiceCollection().BuildServiceProvider();
        var created = factory.TryCreateActivity(new TaskIdentifier("act-fn"), sp, out var activity);

        Assert.True(created);
        Assert.NotNull(activity);
        Assert.Equal(typeof(int), activity!.InputType);
        Assert.Equal(typeof(string), activity.OutputType);

        var result = await activity.RunAsync(new FakeActivityContext(), 7);

        Assert.Equal("v:7", result);
    }

    private sealed record Dependency(string Value);

    private sealed class TestWorkflowWithDependency(Dependency dep) : IWorkflow
    {
        public Dependency Dep { get; } = dep;
        public Type InputType => typeof(object);
        public Type OutputType => typeof(object);

        public Task<object?> RunAsync(WorkflowContext context, object? input) => Task.FromResult<object?>(null);
    }

    private sealed class TestActivityWithDependency(Dependency dep) : IWorkflowActivity
    {
        public Dependency Dep { get; } = dep;
        public Type InputType => typeof(object);
        public Type OutputType => typeof(object);

        public Task<object?> RunAsync(WorkflowActivityContext context, object? input) => Task.FromResult<object?>(null);
    }

    private sealed class ThrowingWorkflow : IWorkflow
    {
        public ThrowingWorkflow() => throw new InvalidOperationException("boom");
        public Type InputType => typeof(object);
        public Type OutputType => typeof(object);
        public Task<object?> RunAsync(WorkflowContext context, object? input) => Task.FromResult<object?>(null);
    }

    private sealed class ThrowingActivity : IWorkflowActivity
    {
        public ThrowingActivity() => throw new InvalidOperationException("boom");
        public Type InputType => typeof(object);
        public Type OutputType => typeof(object);
        public Task<object?> RunAsync(WorkflowActivityContext context, object? input) => Task.FromResult<object?>(null);
    }

    private sealed class FakeWorkflowContext : WorkflowContext
    {
        public override string Name => "wf";
        public override string InstanceId => "i";
        public override DateTime CurrentUtcDateTime => DateTime.UtcNow;
        public override bool IsReplaying => false;

        public override Task<T> CallActivityAsync<T>(string name, object? input = null, WorkflowTaskOptions? options = null) => throw new NotSupportedException();
        public override Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task<T> WaitForExternalEventAsync<T>(string eventName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public override Task<T> WaitForExternalEventAsync<T>(string eventName, TimeSpan timeout) => throw new NotSupportedException();
        public override void SendEvent(string instanceId, string eventName, object payload) => throw new NotSupportedException();
        public override void SetCustomStatus(object? customStatus) => throw new NotSupportedException();
        public override Task<TResult> CallChildWorkflowAsync<TResult>(string workflowName, object? input = null, ChildWorkflowTaskOptions? options = null) => throw new NotSupportedException();
        public override void ContinueAsNew(object? newInput = null, bool preserveUnprocessedEvents = true) => throw new NotSupportedException();
        public override Guid NewGuid() => Guid.NewGuid();
        public override ILogger CreateReplaySafeLogger(string categoryName) => throw new NotSupportedException();
        public override ILogger CreateReplaySafeLogger(Type type) => throw new NotSupportedException();
        public override ILogger CreateReplaySafeLogger<T>() => throw new NotSupportedException();
    }

    private sealed class FakeActivityContext : WorkflowActivityContext
    {
        public override TaskIdentifier Identifier => new("act");
        public override string InstanceId => "i";
    }
}
