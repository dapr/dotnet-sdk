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
using Dapr.Testcontainers.Harnesses;
using Dapr.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow;

public sealed class WorkflowRetryPolicyBackoffTests
{
    private const int MaxAttempts = 3;
    private const double BackoffCoefficient = 2.0;
    private static readonly TimeSpan FirstRetryInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan AllowedSkew = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task ShouldRetryActivityWithBackoff()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("workflow-components");
        var workflowInstanceId = Guid.NewGuid().ToString();

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
            needsActorState: true,
            cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildWorkflow();
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    opt =>
                    {
                        opt.RegisterWorkflow<RetryWithBackoffWorkflow>();
                        opt.RegisterActivity<BackoffTrackingActivity>();
                    },
                    configureClient: (sp, cb) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                            cb.UseGrpcEndpoint(grpcEndpoint);
                    });
            })
            .BuildAndStartAsync();

        BackoffTrackingActivity.Reset(workflowInstanceId);

        using var scope = testApp.CreateScope();
        var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(RetryWithBackoffWorkflow), workflowInstanceId);
        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(
            workflowInstanceId,
            cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var output = result.ReadOutputAs<string>();
        Assert.Equal("Success after retries", output);

        var attemptTimes = BackoffTrackingActivity.GetAttemptTimes(workflowInstanceId);
        Assert.Equal(MaxAttempts, attemptTimes.Length);

        var firstDelay = attemptTimes[1] - attemptTimes[0];
        var secondDelay = attemptTimes[2] - attemptTimes[1];
        var expectedSecondDelay = TimeSpan.FromMilliseconds(
            FirstRetryInterval.TotalMilliseconds * BackoffCoefficient);

        Assert.True(
            firstDelay >= FirstRetryInterval - AllowedSkew,
            $"Expected first retry delay >= {FirstRetryInterval - AllowedSkew}, but was {firstDelay}.");
        Assert.True(
            secondDelay >= expectedSecondDelay - AllowedSkew,
            $"Expected second retry delay >= {expectedSecondDelay - AllowedSkew}, but was {secondDelay}.");
        Assert.True(
            secondDelay >= firstDelay,
            $"Expected backoff to increase between retries. First delay: {firstDelay}. Second delay: {secondDelay}.");
    }

    private sealed class RetryWithBackoffWorkflow : Workflow<string?, string>
    {
        private static readonly WorkflowTaskOptions RetryOptions = new()
        {
            RetryPolicy = new WorkflowRetryPolicy(
                maxNumberOfAttempts: MaxAttempts,
                firstRetryInterval: FirstRetryInterval,
                backoffCoefficient: BackoffCoefficient),
        };

        public override async Task<string> RunAsync(WorkflowContext context, string? input)
        {
            await context.CallActivityAsync<string>(
                nameof(BackoffTrackingActivity),
                null,
                RetryOptions);
            return "Success after retries";
        }
    }

    private sealed class BackoffTrackingActivity : WorkflowActivity<string?, string>
    {
        private static readonly ConcurrentDictionary<string, ConcurrentQueue<DateTimeOffset>> AttemptTimes =
            new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, int> AttemptCounts =
            new(StringComparer.Ordinal);


        public static void Reset(string instanceId)
        {
            AttemptCounts.TryRemove(instanceId, out _);
            AttemptTimes.TryRemove(instanceId, out _);
        }

        public static DateTimeOffset[] GetAttemptTimes(string instanceId) =>
            AttemptTimes.TryGetValue(instanceId, out var times) ? times.ToArray() : [];

        public override Task<string> RunAsync(WorkflowActivityContext context, string? input)
        {
            var attempt = AttemptCounts.AddOrUpdate(context.InstanceId, _ => 1, (_, current) => current + 1);
            AttemptTimes.GetOrAdd(context.InstanceId, _ => new ConcurrentQueue<DateTimeOffset>())
                .Enqueue(DateTimeOffset.UtcNow);

            return attempt < MaxAttempts
                ? throw new InvalidOperationException("Simulated failure")
                : Task.FromResult("OK");
        }
    }
}
