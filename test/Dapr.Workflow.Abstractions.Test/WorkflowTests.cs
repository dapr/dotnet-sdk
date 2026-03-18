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

public class WorkflowTests
{
    private sealed class EchoWorkflow : Workflow<string, string>
    {
        public override Task<string> RunAsync(WorkflowContext context, string input)
            => Task.FromResult($"wf:{input}");
    }

    private sealed class NoopWorkflowContext : WorkflowContext
    {
        public override string Name => "wf";
        public override string InstanceId => "id-1";
        public override DateTime CurrentUtcDateTime => new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public override bool IsReplaying => false;
        public override bool IsPatched(string patchName)
        {
            throw new NotImplementedException();
        }

        public override Task<T> CallActivityAsync<T>(string name, object? input = null, WorkflowTaskOptions? options = null)
            => Task.FromResult(default(T)!);
        public override Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task<T> WaitForExternalEventAsync<T>(string eventName, CancellationToken cancellationToken = default)
            => Task.FromResult(default(T)!);
        public override Task<T> WaitForExternalEventAsync<T>(string eventName, TimeSpan timeout) => Task.FromResult(default(T)!);
        public override void SendEvent(string instanceId, string eventName, object payload) { }
        public override void SetCustomStatus(object? customStatus) { }
        public override Task<TResult> CallChildWorkflowAsync<TResult>(string workflowName, object? input = null, ChildWorkflowTaskOptions? options = null)
            => Task.FromResult(default(TResult)!);
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
    public async Task Generic_Base_Exposes_IWorkflow_Types_And_Runs()
    {
        var wf = new EchoWorkflow();
        var i = (IWorkflow)wf;

        Assert.Equal(typeof(string), i.InputType);
        Assert.Equal(typeof(string), i.OutputType);

        var ctx = new NoopWorkflowContext();
        var result = await i.RunAsync(ctx, "hi");
        Assert.Equal("wf:hi", (string)result!);
    }
}
