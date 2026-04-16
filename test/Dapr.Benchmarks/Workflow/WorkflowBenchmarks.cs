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

using BenchmarkDotNet.Attributes;
using Dapr.Benchmarks.Infrastructure;
using Dapr.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Benchmarks.Workflow;

/// <summary>
/// Benchmarks for Dapr workflow operations (schedule, run, complete) against
/// a real Dapr sidecar via Testcontainers.
/// </summary>
[MemoryDiagnoser]
[MinIterationCount(3)]
[MaxIterationCount(10)]
[IterationCount(5)]
[WarmupCount(1)]
public class WorkflowBenchmarks : DaprBenchmarkBase
{
    private DaprWorkflowClient workflowClient = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        await SetupEnvironmentAsync(
            builder => builder.BuildWorkflow(),
            configureServices: appBuilder =>
            {
                appBuilder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<SimpleWorkflow>();
                        opt.RegisterWorkflow<FanOutWorkflow>();
                        opt.RegisterActivity<DoublingActivity>();
                    },
                    configureClient: (sp, clientBuilder) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                    });
            },
            needsActorState: true);

        workflowClient = Scope!.ServiceProvider.GetRequiredService<DaprWorkflowClient>();
    }

    [GlobalCleanup]
    public async Task Cleanup() => await TeardownEnvironmentAsync();

    [Benchmark(Description = "SimpleWorkflow")]
    public async Task RunSimpleWorkflow()
    {
        var instanceId = Guid.NewGuid().ToString();
        await workflowClient.ScheduleNewWorkflowAsync(nameof(SimpleWorkflow), instanceId, 42);
        await workflowClient.WaitForWorkflowCompletionAsync(instanceId, fetchPayloads: true);
    }

    [Benchmark(Description = "FanOutWorkflow_5Activities")]
    public async Task RunFanOutWorkflow()
    {
        var instanceId = Guid.NewGuid().ToString();
        await workflowClient.ScheduleNewWorkflowAsync(nameof(FanOutWorkflow), instanceId, 5);
        await workflowClient.WaitForWorkflowCompletionAsync(instanceId, fetchPayloads: true);
    }

    /// <summary>
    /// A minimal workflow that calls a single activity.
    /// </summary>
    private sealed class SimpleWorkflow : Workflow<int, int>
    {
        public override async Task<int> RunAsync(WorkflowContext context, int input)
        {
            return await context.CallActivityAsync<int>(nameof(DoublingActivity), input);
        }
    }

    /// <summary>
    /// A workflow that fans out to N parallel activities.
    /// </summary>
    private sealed class FanOutWorkflow : Workflow<int, int[]>
    {
        public override async Task<int[]> RunAsync(WorkflowContext context, int count)
        {
            var tasks = new List<Task<int>>(count);
            for (var i = 0; i < count; i++)
            {
                tasks.Add(context.CallActivityAsync<int>(nameof(DoublingActivity), i));
            }

            return await Task.WhenAll(tasks);
        }
    }

    /// <summary>
    /// A simple activity that doubles its input.
    /// </summary>
    private sealed class DoublingActivity : WorkflowActivity<int, int>
    {
        public override Task<int> RunAsync(WorkflowActivityContext context, int input)
        {
            return Task.FromResult(input * 2);
        }
    }
}
