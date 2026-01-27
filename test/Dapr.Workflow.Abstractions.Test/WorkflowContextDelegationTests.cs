using Dapr.Workflow;

namespace Dapr.Workflow.Abstractions.Test;

public class WorkflowContextDelegationTests
{
    private sealed class ProbeContext : WorkflowContext
    {
        public override string Name => "name";
        public override string InstanceId => "inst-1";
        public override DateTime CurrentUtcDateTime => _now;
        public override bool IsReplaying => false;

        private DateTime _now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public string? LastActivityName { get; private set; }
        public object? LastActivityInput { get; private set; }
        public WorkflowTaskOptions? LastActivityOptions { get; private set; }

        public DateTime? LastTimerFireAt { get; private set; }
        public CancellationToken LastTimerToken { get; private set; }

        public string? LastChildName { get; private set; }
        public object? LastChildInput { get; private set; }
        public ChildWorkflowTaskOptions? LastChildOptions { get; private set; }

        public override Task<T> CallActivityAsync<T>(string name, object? input = null, WorkflowTaskOptions? options = null)
        {
            LastActivityName = name;
            LastActivityInput = input;
            LastActivityOptions = options;
            return Task.FromResult(default(T)!);
        }

        public override Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken)
        {
            LastTimerFireAt = fireAt;
            LastTimerToken = cancellationToken;
            return Task.CompletedTask;
        }

        public override Task<T> WaitForExternalEventAsync<T>(string eventName, CancellationToken cancellationToken = default) => Task.FromResult(default(T)!);
        public override Task<T> WaitForExternalEventAsync<T>(string eventName, TimeSpan timeout) => Task.FromResult(default(T)!);
        public override void SendEvent(string instanceId, string eventName, object payload) { }
        public override void SetCustomStatus(object? customStatus) { }

        public override Task<TResult> CallChildWorkflowAsync<TResult>(string workflowName, object? input = null, ChildWorkflowTaskOptions? options = null)
        {
            LastChildName = workflowName;
            LastChildInput = input;
            LastChildOptions = options;
            return Task.FromResult(default(TResult)!);
        }

        public override Microsoft.Extensions.Logging.ILogger CreateReplaySafeLogger(string categoryName) => new NullLogger();
        public override Microsoft.Extensions.Logging.ILogger CreateReplaySafeLogger(Type type) => new NullLogger();
        public override Microsoft.Extensions.Logging.ILogger CreateReplaySafeLogger<T>() => new NullLogger();
        public override void ContinueAsNew(object? newInput = null, bool preserveUnprocessedEvents = true) { }
        public override Guid NewGuid() => Guid.Empty;

        private sealed class NullLogger : Microsoft.Extensions.Logging.ILogger
        {
            IDisposable? Microsoft.Extensions.Logging.ILogger.BeginScope<TState>(TState state) => null;
            bool Microsoft.Extensions.Logging.ILogger.IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
            void Microsoft.Extensions.Logging.ILogger.Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
        }
    }

    [Fact]
    public async Task CallActivityAsync_NonGeneric_Delegates_To_Generic()
    {
        var ctx = new ProbeContext();
        var options = new WorkflowTaskOptions();
        await ctx.CallActivityAsync("act", 123, options);

        Assert.Equal("act", ctx.LastActivityName);
        Assert.Equal(123, ctx.LastActivityInput);
        Assert.Same(options, ctx.LastActivityOptions);
    }

    [Fact]
    public async Task CreateTimer_With_TimeSpan_Delegates_To_DateTime_Using_CurrentUtc()
    {
        var ctx = new ProbeContext();
        var delay = TimeSpan.FromMinutes(5);
        await ctx.CreateTimer(delay, CancellationToken.None);

        Assert.Equal(new DateTime(2025, 1, 1, 0, 5, 0, DateTimeKind.Utc), ctx.LastTimerFireAt);
    }

    [Fact]
    public async Task CallChildWorkflowAsync_NonGeneric_Delegates_To_Generic()
    {
        var ctx = new ProbeContext();
        var options = new ChildWorkflowTaskOptions();
        await ctx.CallChildWorkflowAsync("child", new { X = 1 }, options);

        Assert.Equal("child", ctx.LastChildName);
        Assert.NotNull(ctx.LastChildInput);
        Assert.Same(options, ctx.LastChildOptions);
    }
}
