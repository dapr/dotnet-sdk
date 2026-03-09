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
using Microsoft.Extensions.Logging.Abstractions;
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
    public void RegisterActivity_Generic_ShouldUseProvidedName_WhenNameIsSpecified()
    {
        var factory = new WorkflowsFactory(NullLogger<WorkflowsFactory>.Instance);

        factory.RegisterActivity<TestActivityA>("custom-activity");

        var sp = new ServiceCollection().BuildServiceProvider();

        Assert.True(factory.TryCreateActivity(new TaskIdentifier("custom-activity"), sp, out var activity));
        Assert.NotNull(activity);
        Assert.IsType<TestActivityA>(activity);

        Assert.False(factory.TryCreateActivity(new TaskIdentifier(nameof(TestActivityA)), sp, out _));
    }

    [Fact]
    public void RegisterWorkflow_Generic_ShouldBeCaseInsensitive_ForLookup()
    {
        var factory = new WorkflowsFactory(NullLogger<WorkflowsFactory>.Instance);

        factory.RegisterWorkflow<TestWorkflowA>("MyWorkflow");

        var sp = new ServiceCollection().BuildServiceProvider();

        Assert.True(factory.TryCreateWorkflow(new TaskIdentifier("myworkflow"), sp, out var workflow));
        Assert.NotNull(workflow);
        Assert.IsType<TestWorkflowA>(workflow);
    }
    
    [Fact]
    public void RegisterWorkflow_Generic_ShouldUseProvidedName_WhenNameIsSpecified()
    {
        var factory = new WorkflowsFactory(NullLogger<WorkflowsFactory>.Instance);

        factory.RegisterWorkflow<TestWorkflowA>("custom-workflow");

        var sp = new ServiceCollection().BuildServiceProvider();

        Assert.True(factory.TryCreateWorkflow(new TaskIdentifier("custom-workflow"), sp, out var workflow));
        Assert.NotNull(workflow);
        Assert.IsType<TestWorkflowA>(workflow);

        Assert.False(factory.TryCreateWorkflow(new TaskIdentifier(nameof(TestWorkflowA)), sp, out _));
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
    public void RegisterWorkflow_Generic_ShouldNotOverwrite_WhenRegisteringSameNameTwice()
    {
        var factory = new WorkflowsFactory(NullLogger<WorkflowsFactory>.Instance);

        factory.RegisterWorkflow<TestWorkflowA>("wf");
        factory.RegisterWorkflow<TestWorkflowB>("wf"); // should be ignored (TryAdd)

        var sp = new ServiceCollection().BuildServiceProvider();

        Assert.True(factory.TryCreateWorkflow(new TaskIdentifier("wf"), sp, out var workflow));
        Assert.NotNull(workflow);
        Assert.IsType<TestWorkflowA>(workflow);
    }

    [Fact]
    public void RegisterActivity_Generic_ShouldNotOverwrite_WhenRegisteringSameNameTwice()
    {
        var factory = new WorkflowsFactory(NullLogger<WorkflowsFactory>.Instance);

        factory.RegisterActivity<TestActivityA>("act");
        factory.RegisterActivity<TestActivityB>("act"); // should be ignored (TryAdd)

        var sp = new ServiceCollection().BuildServiceProvider();

        Assert.True(factory.TryCreateActivity(new TaskIdentifier("act"), sp, out var activity));
        Assert.NotNull(activity);
        Assert.IsType<TestActivityA>(activity);
    }

    [Fact]
    public void RegisterWorkflow_Function_ShouldThrowArgumentNullException_WhenImplementationIsNull()
    {
        var logger = Mock.Of<ILogger<WorkflowsFactory>>();
        var factory = new WorkflowsFactory(logger);

        Assert.Throws<ArgumentNullException>(() => factory.RegisterWorkflow<int, int>("wf", null!));
    }
    
    [Fact]
    public void RegisterActivity_Generic_ShouldBeCaseInsensitive_ForLookup()
    {
        var factory = new WorkflowsFactory(NullLogger<WorkflowsFactory>.Instance);

        factory.RegisterActivity<TestActivityA>("MyActivity");

        var sp = new ServiceCollection().BuildServiceProvider();

        Assert.True(factory.TryCreateActivity(new TaskIdentifier("myactivity"), sp, out var activity));
        Assert.NotNull(activity);
        Assert.IsType<TestActivityA>(activity);
    }

    [Fact]
    public async Task RegisterWorkflow_Function_ShouldNotOverwrite_WhenRegisteringSameNameTwice()
    {
        var factory = new WorkflowsFactory(NullLogger<WorkflowsFactory>.Instance);

        factory.RegisterWorkflow<int, int>("wf", (_, x) => Task.FromResult(x + 1));
        factory.RegisterWorkflow<int, int>("wf", (_, x) => Task.FromResult(x + 999)); // should be ignored (TryAdd)

        var sp = new ServiceCollection().BuildServiceProvider();

        Assert.True(factory.TryCreateWorkflow(new TaskIdentifier("wf"), sp, out var workflow));
        Assert.NotNull(workflow);

        var result = await workflow!.RunAsync(new FakeWorkflowContext(), 10);

        Assert.Equal(11, result);
    }

    [Fact]
    public async Task RegisterActivity_Function_ShouldNotOverwrite_WhenRegisteringSameNameTwice()
    {
        var factory = new WorkflowsFactory(NullLogger<WorkflowsFactory>.Instance);

        factory.RegisterActivity<int, int>("act", (_, x) => Task.FromResult(x + 1));
        factory.RegisterActivity<int, int>("act", (_, x) => Task.FromResult(x + 999)); // should be ignored (TryAdd)

        var sp = new ServiceCollection().BuildServiceProvider();

        Assert.True(factory.TryCreateActivity(new TaskIdentifier("act"), sp, out var activity));
        Assert.NotNull(activity);

        var result = await activity!.RunAsync(new FakeActivityContext(), 10);

        Assert.Equal(11, result);
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
    
    private sealed class TestWorkflowA : IWorkflow
    {
        public Type InputType => typeof(object);
        public Type OutputType => typeof(object);
        public Task<object?> RunAsync(WorkflowContext context, object? input) => Task.FromResult<object?>(null);
    }

    private sealed class TestWorkflowB : IWorkflow
    {
        public Type InputType => typeof(object);
        public Type OutputType => typeof(object);
        public Task<object?> RunAsync(WorkflowContext context, object? input) => Task.FromResult<object?>(null);
    }

    private sealed class TestActivityA : IWorkflowActivity
    {
        public Type InputType => typeof(object);
        public Type OutputType => typeof(object);
        public Task<object?> RunAsync(WorkflowActivityContext context, object? input) => Task.FromResult<object?>(null);
    }

    private sealed class TestActivityB : IWorkflowActivity
    {
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
        public override bool IsPatched(string patchName) => true;

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
        public override string TaskExecutionKey => "test-key";
        public override string InstanceId => "i";
    }
}
