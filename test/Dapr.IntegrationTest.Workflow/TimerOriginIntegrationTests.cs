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
using Dapr.Testcontainers.Harnesses;
using Dapr.Testcontainers.Xunit.Attributes;
using Dapr.Workflow;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow;

/// <summary>
/// Integration tests that exercise timer-origin scenarios end-to-end against a
/// real Dapr sidecar.  Every test is gated to Dapr ≥ 1.18 because the runtime
/// must understand the new origin fields.
/// </summary>
public sealed class TimerOriginIntegrationTests
{
    // ------------------------------------------------------------------
    //  1. CreateTimer completes the workflow after the timer fires
    // ------------------------------------------------------------------

    [MinimumDaprRuntimeFact("1.18")]
    public async Task CreateTimer_ShouldCompleteWorkflow_AfterTimerFires()
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
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<TimerOnlyWorkflow>();
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

        using var scope = testApp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await client.ScheduleNewWorkflowAsync(
            nameof(TimerOnlyWorkflow), workflowInstanceId);

        var result = await client.WaitForWorkflowCompletionAsync(
            workflowInstanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal("timer-fired", result.ReadOutputAs<string>());
    }

    // ------------------------------------------------------------------
    //  2. WaitForExternalEvent + finite timeout → event arrives in time
    // ------------------------------------------------------------------

    [MinimumDaprRuntimeFact("1.18")]
    public async Task WaitForExternalEvent_FiniteTimeout_ShouldComplete_WhenEventArrivesBeforeTimeout()
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
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<ExternalEventWithTimeoutWorkflow>();
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

        using var scope = testApp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await client.ScheduleNewWorkflowAsync(
            nameof(ExternalEventWithTimeoutWorkflow), workflowInstanceId);

        // Give the workflow a moment to start and begin waiting
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        // Raise the event well before the 60-second timeout
        await client.RaiseEventAsync(
            workflowInstanceId, "approval", "approved",
            TestContext.Current.CancellationToken);

        var result = await client.WaitForWorkflowCompletionAsync(
            workflowInstanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal("approved", result.ReadOutputAs<string>());
    }

    // ------------------------------------------------------------------
    //  3. WaitForExternalEvent + finite timeout → times out
    // ------------------------------------------------------------------

    [MinimumDaprRuntimeFact("1.18")]
    public async Task WaitForExternalEvent_FiniteTimeout_ShouldTimeout_WhenNoEventArrives()
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
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<ExternalEventShortTimeoutWorkflow>();
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

        using var scope = testApp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await client.ScheduleNewWorkflowAsync(
            nameof(ExternalEventShortTimeoutWorkflow), workflowInstanceId);

        // Don't raise the event — let the timeout expire
        var result = await client.WaitForWorkflowCompletionAsync(
            workflowInstanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal("timed-out", result.ReadOutputAs<string>());
    }

    // ------------------------------------------------------------------
    //  4. WaitForExternalEvent without timeout (indefinite) completes
    //     when the event arrives
    // ------------------------------------------------------------------

    [MinimumDaprRuntimeFact("1.18")]
    public async Task WaitForExternalEvent_Indefinite_ShouldComplete_WhenEventArrives()
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
                    configureRuntime: opt =>
                    {
                        opt.RegisterWorkflow<IndefiniteExternalEventWorkflow>();
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

        using var scope = testApp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();

        await client.ScheduleNewWorkflowAsync(
            nameof(IndefiniteExternalEventWorkflow), workflowInstanceId);

        // Give the workflow time to start and begin waiting
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        // Raise the event
        await client.RaiseEventAsync(
            workflowInstanceId, "signal", "go",
            TestContext.Current.CancellationToken);

        var result = await client.WaitForWorkflowCompletionAsync(
            workflowInstanceId, cancellation: TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal("go", result.ReadOutputAs<string>());
    }

    // ==================================================================
    // Workflow definitions
    // ==================================================================

    /// <summary>
    /// Workflow that creates a short timer (with TimerOriginCreateTimer origin)
    /// and completes after it fires.
    /// </summary>
    private sealed class TimerOnlyWorkflow : Workflow<object?, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, object? input)
        {
            await context.CreateTimer(TimeSpan.FromSeconds(3));
            return "timer-fired";
        }
    }

    /// <summary>
    /// Workflow that waits for an external event with a generous finite timeout.
    /// The test raises the event before the timeout, so it should return the event
    /// payload rather than timing out.
    /// </summary>
    private sealed class ExternalEventWithTimeoutWorkflow : Workflow<object?, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, object? input)
        {
            try
            {
                var data = await context.WaitForExternalEventAsync<string>(
                    "approval", TimeSpan.FromSeconds(60));
                return data;
            }
            catch (TaskCanceledException)
            {
                return "timed-out";
            }
        }
    }

    /// <summary>
    /// Workflow that waits for an external event with a very short timeout so
    /// the timer fires before any event is raised.
    /// </summary>
    private sealed class ExternalEventShortTimeoutWorkflow : Workflow<object?, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, object? input)
        {
            try
            {
                var data = await context.WaitForExternalEventAsync<string>(
                    "approval", TimeSpan.FromSeconds(5));
                return data;
            }
            catch (TaskCanceledException)
            {
                return "timed-out";
            }
        }
    }

    /// <summary>
    /// Workflow that waits indefinitely for an external event (no timeout).
    /// The timer origin implementation emits a synthetic optional timer with
    /// the sentinel fireAt value.
    /// </summary>
    private sealed class IndefiniteExternalEventWorkflow : Workflow<object?, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, object? input)
        {
            var data = await context.WaitForExternalEventAsync<string>("signal");
            return data;
        }
    }
}
