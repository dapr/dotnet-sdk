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
//  ------------------------------------------------------------------------

using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Testing;
using Dapr.Testcontainers.Harnesses;
using Dapr.Testcontainers.Xunit.Attributes;
using Dapr.Workflow;
using Dapr.Workflow.Client;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow;

public sealed class WorkflowScheduleTests
{
    [MinimumDaprRuntimeFact("1.20")]
    public async Task ScheduledRunsCompleteTheSchedule()
    {
        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true, cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);
        await using var testApp = await StartAppAsync(environment);
        using var scope = testApp.CreateScope();
        var client = await GetConnectedClientAsync(scope);
        var name = NewName();

        await client.CreateWorkflowScheduleAsync(name, nameof(EchoWorkflow), "@every 1s",
            new WorkflowScheduleOptions { Input = "hello", Repeats = 2 }, TestContext.Current.CancellationToken);

        var schedule = await WaitForScheduleAsync(client, name, s => s.Status is
        {
            State: WorkflowScheduleState.Completed,
            LastRun.RuntimeStatus: WorkflowRuntimeStatus.Completed
        });
        Assert.Equal(2, schedule.Status!.RunCount);
        Assert.Null(schedule.Status.NextRunTime);
        Assert.NotNull(schedule.Status.LastRunTime);
        Assert.StartsWith($"{name}_", schedule.Status.LastRun!.InstanceId);
        Assert.Contains("hello", schedule.SerializedInput);
        Assert.Contains("hello", schedule.Status.LastRun.SerializedOutput);

        var runs = await client.ListInstanceIdsAsync(instanceIdPrefix: $"{name}_", cancellation: TestContext.Current.CancellationToken);
        Assert.Equal(2, runs.InstanceIds.Count);

        var ex = await Assert.ThrowsAsync<RpcException>(() => client.TriggerWorkflowScheduleAsync(name, TestContext.Current.CancellationToken));
        Assert.Equal(StatusCode.FailedPrecondition, ex.StatusCode);

        foreach (var runId in runs.InstanceIds)
        {
            await client.WaitForWorkflowCompletionAsync(runId, cancellation: TestContext.Current.CancellationToken);
            await client.PurgeInstanceAsync(runId, TestContext.Current.CancellationToken);
        }
    }

    [MinimumDaprRuntimeFact("1.20")]
    public async Task TriggerStartsManualRun()
    {
        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true, cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);
        await using var testApp = await StartAppAsync(environment);
        using var scope = testApp.CreateScope();
        var client = await GetConnectedClientAsync(scope);
        var name = NewName();

        await client.CreateWorkflowScheduleAsync(name, nameof(EchoWorkflow), "@every 1h",
            new WorkflowScheduleOptions { Input = "hello" }, TestContext.Current.CancellationToken);

        var runId = await client.TriggerWorkflowScheduleAsync(name, TestContext.Current.CancellationToken);
        Assert.StartsWith($"{name}_", runId);
        Assert.EndsWith("_manual", runId);
        var run = await client.WaitForWorkflowCompletionAsync(runId, cancellation: TestContext.Current.CancellationToken);
        Assert.Equal(WorkflowRuntimeStatus.Completed, run.RuntimeStatus);

        var schedule = await WaitForScheduleAsync(client, name, s => s.Status?.RunCount == 1);
        Assert.Equal(WorkflowScheduleState.Active, schedule.Status!.State);

        await client.CreateWorkflowScheduleAsync(name, nameof(EchoWorkflow), "@every 1h",
            new WorkflowScheduleOptions { Overwrite = true }, TestContext.Current.CancellationToken);
        var overwritten = await client.GetWorkflowScheduleAsync(name, TestContext.Current.CancellationToken);
        Assert.NotNull(overwritten);
        Assert.Equal(0, overwritten.Status!.RunCount);

        await client.PurgeInstanceAsync(runId, TestContext.Current.CancellationToken);
    }

    [MinimumDaprRuntimeFact("1.20")]
    public async Task CreateOverwriteDeleteAndErrors()
    {
        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true, cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);
        await using var testApp = await StartAppAsync(environment);
        using var scope = testApp.CreateScope();
        var client = await GetConnectedClientAsync(scope);
        var name = NewName();

        await client.CreateWorkflowScheduleAsync(name, nameof(EchoWorkflow), "@every 1h", cancellation: TestContext.Current.CancellationToken);
        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            client.CreateWorkflowScheduleAsync(name, nameof(EchoWorkflow), "@every 1h", cancellation: TestContext.Current.CancellationToken));
        Assert.Equal(StatusCode.AlreadyExists, ex.StatusCode);

        await client.CreateWorkflowScheduleAsync(name, nameof(EchoWorkflow), "@every 2h",
            new WorkflowScheduleOptions { Overwrite = true }, TestContext.Current.CancellationToken);
        var schedule = await client.GetWorkflowScheduleAsync(name, TestContext.Current.CancellationToken);
        Assert.NotNull(schedule);
        Assert.Equal("@every 2h", schedule.Schedule);

        await client.DeleteWorkflowScheduleAsync(name, TestContext.Current.CancellationToken);
        Assert.Null(await client.GetWorkflowScheduleAsync(name, TestContext.Current.CancellationToken));
        ex = await Assert.ThrowsAsync<RpcException>(() => client.DeleteWorkflowScheduleAsync(name, TestContext.Current.CancellationToken));
        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
        ex = await Assert.ThrowsAsync<RpcException>(() => client.TriggerWorkflowScheduleAsync(name, TestContext.Current.CancellationToken));
        Assert.Equal(StatusCode.NotFound, ex.StatusCode);

        ex = await Assert.ThrowsAsync<RpcException>(() =>
            client.CreateWorkflowScheduleAsync(name, nameof(EchoWorkflow), "not a cron", cancellation: TestContext.Current.CancellationToken));
        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
        ex = await Assert.ThrowsAsync<RpcException>(() =>
            client.CreateWorkflowScheduleAsync("dapr-internal-schedule-x", nameof(EchoWorkflow), "@every 1h", cancellation: TestContext.Current.CancellationToken));
        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [MinimumDaprRuntimeFact("1.20")]
    public async Task ListWorkflowSchedulesPaginates()
    {
        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(needsActorState: true, cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);
        await using var testApp = await StartAppAsync(environment);
        using var scope = testApp.CreateScope();
        var client = await GetConnectedClientAsync(scope);
        var names = new[] { NewName(), NewName(), NewName() };

        foreach (var name in names)
        {
            await client.CreateWorkflowScheduleAsync(name, nameof(EchoWorkflow), "@every 1h", cancellation: TestContext.Current.CancellationToken);
        }

        var listed = new List<string>();
        string? continuationToken = null;
        do
        {
            var page = await client.ListWorkflowSchedulesAsync(continuationToken, 2, TestContext.Current.CancellationToken);
            Assert.True(page.Schedules.Count <= 2);
            listed.AddRange(page.Schedules.Select(s => s.Name));
            continuationToken = page.ContinuationToken;
        } while (continuationToken is not null);

        Assert.Equal(3, listed.Count);
        Assert.All(names, name => Assert.Contains(name, listed));

        foreach (var name in names)
        {
            await client.DeleteWorkflowScheduleAsync(name, TestContext.Current.CancellationToken);
        }
    }

    private static async Task<DaprTestApplication> StartAppAsync(DaprTestEnvironment environment)
    {
        var harness = new DaprHarnessBuilder(TestDirectoryManager.CreateTestDirectory("workflow-components"))
            .WithEnvironment(environment)
            .BuildWorkflow();
        return await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    opt => opt.RegisterWorkflow<EchoWorkflow>(),
                    configureClient: (sp, cb) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                            cb.UseGrpcEndpoint(grpcEndpoint);
                    });
            })
            .BuildAndStartAsync();
    }

    // Runs one workflow to completion so the worker is connected before the schedule needs it.
    private static async Task<DaprWorkflowClient> GetConnectedClientAsync(IServiceScope scope)
    {
        var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();
        var warmUpId = Guid.NewGuid().ToString();
        await client.ScheduleNewWorkflowAsync(nameof(EchoWorkflow), warmUpId, "warm-up");
        await client.WaitForWorkflowCompletionAsync(warmUpId, cancellation: TestContext.Current.CancellationToken);
        return client;
    }

    private static async Task<WorkflowSchedule> WaitForScheduleAsync(
        DaprWorkflowClient client, string name, Func<WorkflowSchedule, bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, TestContext.Current.CancellationToken);
        while (true)
        {
            var schedule = await client.GetWorkflowScheduleAsync(name, linked.Token);
            if (schedule is not null && condition(schedule))
            {
                return schedule;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), linked.Token);
        }
    }

    private static string NewName() => "it-" + Guid.NewGuid().ToString("N")[..8];

    private sealed class EchoWorkflow : Workflow<string, string>
    {
        public override Task<string> RunAsync(WorkflowContext context, string input) => Task.FromResult(input);
    }
}
