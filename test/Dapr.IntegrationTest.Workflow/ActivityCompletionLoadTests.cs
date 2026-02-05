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

using System.Collections.Concurrent;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Harnesses;
using Dapr.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dapr.IntegrationTest.Workflow;

public sealed class ActivityCompletionLoadTests
{
    [Fact]
    public async Task ActivityCompletions_ShouldBeAcknowledged_UnderLoad()
    {
        const int activityCount = 50;
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");
        var workflowInstanceId = Guid.NewGuid().ToString();
        var loggerProvider = new InMemoryLoggerProvider();

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true);
        await environment.StartAsync();

        var harness = new DaprHarnessBuilder(componentsDir).BuildWorkflow();
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<FanOutWorkflow>();
                        opt.RegisterActivity<CountingActivity>();
                    },
                    configureClient: (sp, clientBuilder) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                    });
                builder.Logging.AddProvider(loggerProvider);
            })
            .BuildAndStartAsync();

        CountingActivity.Reset(workflowInstanceId);

        using var scope = testApp.CreateScope();
        var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(FanOutWorkflow), workflowInstanceId, activityCount);
        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(workflowInstanceId, true);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var completedCount = result.ReadOutputAs<int>();
        Assert.Equal(activityCount, completedCount);

        for (var i = 0; i < activityCount; i++)
        {
            Assert.True(CountingActivity.GetCount(workflowInstanceId, i) >= 1);
        }

        var unexpectedWarnings = loggerProvider.Entries
            .Where(entry => entry.Level >= LogLevel.Warning)
            .Where(entry => entry.Message.Contains("Received completion for unknown taskId", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(unexpectedWarnings);
    }

    private sealed class CountingActivity : WorkflowActivity<int, int>
    {
        private static readonly ConcurrentDictionary<string, int> Counts = new(StringComparer.Ordinal);

        public override Task<int> RunAsync(WorkflowActivityContext context, int input)
        {
            var key = BuildKey(context.InstanceId, input);
            var count = Counts.AddOrUpdate(key, _ => 1, (_, current) => current + 1);
            return Task.FromResult(count);
        }

        public static void Reset(string instanceId)
        {
            foreach (var key in Counts.Keys.Where(k => k.StartsWith(instanceId + ":", StringComparison.Ordinal)))
            {
                Counts.TryRemove(key, out _);
            }
        }

        public static int GetCount(string instanceId, int input) => Counts.GetValueOrDefault(BuildKey(instanceId, input), 0);

        private static string BuildKey(string instanceId, int input) => $"{instanceId}:{input}";
    }

    private sealed class FanOutWorkflow : Workflow<int, int>
    {
        public override async Task<int> RunAsync(WorkflowContext context, int input)
        {
            var tasks = new Task<int>[input];
            for (var i = 0; i < input; i++)
            {
                tasks[i] = context.CallActivityAsync<int>(nameof(CountingActivity), i);
            }

            var results = await Task.WhenAll(tasks);
            return results.Length;
        }
    }

    private sealed class InMemoryLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();

        public IEnumerable<LogEntry> Entries => _entries;

        public ILogger CreateLogger(string categoryName) => new InMemoryLogger(categoryName, _entries);

        public void Dispose()
        {
        }

        internal sealed record LogEntry(LogLevel Level, string Category, string Message, Exception? Exception);

        private sealed class InMemoryLogger(string categoryName, ConcurrentQueue<LogEntry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (formatter is null)
                    return;

                var message = formatter(state, exception);
                entries.Enqueue(new LogEntry(logLevel, categoryName, message, exception));
            }
        }
    }
}
