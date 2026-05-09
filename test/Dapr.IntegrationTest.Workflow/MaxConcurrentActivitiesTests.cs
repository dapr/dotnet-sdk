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

using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Harnesses;
using Dapr.Testcontainers.Xunit.Attributes;
using Dapr.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow;

public sealed class MaxConcurrentActivitiesTests
{
    /// <summary>
    /// Verifies that <see cref="WorkflowRuntimeOptions.MaxConcurrentActivities"/> = 1 limits
    /// activity execution to a single concurrent activity even when the workflow fans out more.
    /// </summary>
    [MinimumDaprRuntimeFact("1.17")]
    public async Task ShouldRespectMaxConcurrentActivitiesLimitOfOne()
    {
        const int limit = 1;
        const int activityCount = 5;

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
                    configureRuntime: opt =>
                    {
                        opt.MaxConcurrentActivities = limit;
                        opt.RegisterWorkflow<FanOutWorkflow>();
                        opt.RegisterActivity<ConcurrencyTrackingActivity>();
                    },
                    configureClient: (sp, clientBuilder) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                    });
            })
            .BuildAndStartAsync();

        ConcurrencyTrackingActivity.Reset();

        using var scope = testApp.CreateScope();
        var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(FanOutWorkflow), workflowInstanceId, activityCount);
        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(
            workflowInstanceId, true, TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.True(
            ConcurrencyTrackingActivity.MaxObservedConcurrency <= limit,
            $"Expected max concurrent activities <= {limit}, but observed {ConcurrencyTrackingActivity.MaxObservedConcurrency}");
    }

    /// <summary>
    /// Verifies that <see cref="WorkflowRuntimeOptions.MaxConcurrentActivities"/> = 3 allows up to
    /// 3 concurrent activities and that all activities complete successfully.
    /// </summary>
    [MinimumDaprRuntimeFact("1.17")]
    public async Task ShouldRespectMaxConcurrentActivitiesLimitOfThree()
    {
        const int limit = 3;
        const int activityCount = 10;

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
                    configureRuntime: opt =>
                    {
                        opt.MaxConcurrentActivities = limit;
                        opt.RegisterWorkflow<FanOutWorkflow>();
                        opt.RegisterActivity<ConcurrencyTrackingActivity>();
                    },
                    configureClient: (sp, clientBuilder) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                    });
            })
            .BuildAndStartAsync();

        ConcurrencyTrackingActivity.Reset();

        using var scope = testApp.CreateScope();
        var daprWorkflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await daprWorkflowClient.ScheduleNewWorkflowAsync(nameof(FanOutWorkflow), workflowInstanceId, activityCount);
        var result = await daprWorkflowClient.WaitForWorkflowCompletionAsync(
            workflowInstanceId, true, TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.True(
            ConcurrencyTrackingActivity.MaxObservedConcurrency <= limit,
            $"Expected max concurrent activities <= {limit}, but observed {ConcurrencyTrackingActivity.MaxObservedConcurrency}");
    }

    /// <summary>
    /// Tracks the maximum number of concurrently executing activity instances using a shared
    /// static counter. Each activity holds for a brief period so concurrent executions can
    /// accumulate and be observed.
    /// </summary>
    private sealed class ConcurrencyTrackingActivity : WorkflowActivity<int, int>
    {
        private static int _currentConcurrent;
        private static int _maxObservedConcurrent;
        private static readonly object Lock = new();

        public static int MaxObservedConcurrency
        {
            get
            {
                lock (Lock)
                {
                    return _maxObservedConcurrent;
                }
            }
        }

        public static void Reset()
        {
            lock (Lock)
            {
                _currentConcurrent = 0;
                _maxObservedConcurrent = 0;
            }
        }

        public override async Task<int> RunAsync(WorkflowActivityContext context, int input)
        {
            lock (Lock)
            {
                _currentConcurrent++;
                if (_currentConcurrent > _maxObservedConcurrent)
                    _maxObservedConcurrent = _currentConcurrent;
            }

            // Hold briefly so any concurrent activities can be observed accumulating.
            await Task.Delay(TimeSpan.FromMilliseconds(300));

            lock (Lock)
            {
                _currentConcurrent--;
            }

            return input;
        }
    }

    private sealed class FanOutWorkflow : Workflow<int, int[]>
    {
        public override async Task<int[]> RunAsync(WorkflowContext context, int input)
        {
            var tasks = new Task<int>[input];
            for (var i = 0; i < input; i++)
            {
                tasks[i] = context.CallActivityAsync<int>(nameof(ConcurrencyTrackingActivity), i);
            }

            return await Task.WhenAll(tasks);
        }
    }
}
