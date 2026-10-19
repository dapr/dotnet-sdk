// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Dapr.DurableTask.Protobuf;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Harnesses;
using Dapr.Testcontainers.Xunit.Attributes;
using Dapr.Workflow;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow;

public sealed class WorkflowReliabilityTests
{
    [MinimumDaprRuntimeTheory("1.17")]
    [InlineData("workflow")]
    [InlineData("activity")]
    [InlineData("history")]
    public async Task TransportFailure_ReconnectsAndCompletes_WithRegisteredSerializer(string failure)
    {
        var fault = new FailOnceInterceptor(failure);
        await RunAsync(async (client, token) =>
        {
            var id = Guid.NewGuid().ToString();
            await DuringPlacementChanges(() => client.ScheduleNewWorkflowAsync(nameof(EchoWorkflow), id,
                new Payment(42), startTime: null, cancellation: token), token);
            var result = await DuringPlacementChanges(
                () => client.WaitForWorkflowCompletionAsync(id, cancellation: token), token);
            Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
            Assert.Equal(42, result.ReadOutputAs<Payment>()!.PaymentAmount);
            Assert.Equal(1, fault.Failures);
            Assert.True(fault.Connections >= 2, "The failed delivery must retire its stream for redelivery.");
        }, fault);
    }

    [MinimumDaprRuntimeFact("1.17")]
    public async Task SuspendedEvents_SurviveResumeAndContinueAsNew_WithAbandonedWaiter()
    {
        await RunAsync(async (client, token) =>
        {
            var id = await client.ScheduleNewWorkflowAsync(nameof(RacingEventsWorkflow), Guid.NewGuid().ToString(),
                new RaceInput(null, 0), startTime: null, cancellation: token);
            await client.WaitForWorkflowStartAsync(id, cancellation: token);
            await client.SuspendWorkflowAsync(id, cancellation: token);
            await WaitForStatus(client, id, WorkflowRuntimeStatus.Suspended, token);
            await client.RaiseEventAsync(id, "a", 1, token);
            await client.RaiseEventAsync(id, "b", 2, token);
            Assert.Equal(WorkflowRuntimeStatus.Suspended,
                (await client.GetWorkflowStateAsync(id, cancellation: token))!.RuntimeStatus);
            await client.ResumeWorkflowAsync(id, cancellation: token);
            var result = await client.WaitForWorkflowCompletionAsync(id, cancellation: token);
            Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
            Assert.Equal(3, result.ReadOutputAs<int>());
        });
    }

    [MinimumDaprRuntimeFact("1.17")]
    public async Task RetryDeadline_DoesNotScheduleAnAttemptAfterItsDelay()
    {
        var attempts = new AttemptCounter();
        await RunAsync(async (client, token) =>
        {
            var id = await client.ScheduleNewWorkflowAsync(nameof(DeadlineWorkflow), Guid.NewGuid().ToString(),
                null, startTime: null, cancellation: token);
            var result = await client.WaitForWorkflowCompletionAsync(id, cancellation: token);
            Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
            Assert.Equal("expired", result.ReadOutputAs<string>());
            Assert.Equal(1, attempts.Count);
        }, attempts: attempts);
    }

    private static async Task RunAsync(Func<DaprWorkflowClient, CancellationToken, Task> test,
        FailOnceInterceptor? fault = null, AttemptCounter? attempts = null)
    {
        var components = TestDirectoryManager.CreateTestDirectory("workflow-reliability-components");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
            needsActorState: true, cancellationToken: timeout.Token);
        await environment.StartAsync(timeout.Token);
        var harness = new DaprHarnessBuilder(components).WithEnvironment(environment).BuildWorkflow();
        await using var app = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddSingleton(attempts ?? new AttemptCounter());
                builder.Services.AddDaprWorkflowBuilder(options =>
                {
                    options.RegisterWorkflow<EchoWorkflow>();
                    options.RegisterWorkflow<RacingEventsWorkflow>();
                    options.RegisterWorkflow<DeadlineWorkflow>();
                    options.RegisterActivity<EchoActivity>();
                    options.RegisterActivity<FailingActivity>();
                }, configureClient: (provider, client) =>
                {
                    var endpoint = provider.GetRequiredService<IConfiguration>()["DAPR_GRPC_ENDPOINT"];
                    if (!string.IsNullOrEmpty(endpoint))
                        client.UseGrpcEndpoint(endpoint);
                }).WithJsonSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });
                if (fault is not null)
                    builder.Services.AddGrpcClient<TaskHubSidecarService.TaskHubSidecarServiceClient>()
                        .AddInterceptor(() => fault);
            }).BuildAndStartAsync();
        using var scope = app.CreateScope();
        await test(scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>(), timeout.Token);
    }

    private static async Task WaitForStatus(DaprWorkflowClient client, string id,
        WorkflowRuntimeStatus status, CancellationToken token)
    {
        while ((await client.GetWorkflowStateAsync(id, cancellation: token))?.RuntimeStatus != status)
            await Task.Delay(100, token);
    }

    private static async Task<T> DuringPlacementChanges<T>(Func<Task<T>> operation, CancellationToken token)
    {
        while (true)
        {
            try { return await operation(); }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition &&
                                         ex.Status.Detail.Contains("did not find address for actor", StringComparison.Ordinal))
            {
                // Stream retirement briefly unregisters this worker's workflow actors.
                await Task.Delay(200, token);
            }
        }
    }

    public sealed record Payment(int PaymentAmount);
    public sealed record RaceInput(string? RemainingEvent, int Total);
    private sealed class AttemptCounter { public int Count; }

    private sealed class EchoWorkflow : Workflow<Payment, Payment>
    {
        public override Task<Payment> RunAsync(WorkflowContext context, Payment input) =>
            context.CallActivityAsync<Payment>(nameof(EchoActivity), input);
    }
    private sealed class EchoActivity : WorkflowActivity<Payment, Payment>
    {
        public override Task<Payment> RunAsync(WorkflowActivityContext context, Payment input) => Task.FromResult(input);
    }
    private sealed class RacingEventsWorkflow : Workflow<RaceInput, int>
    {
        public override async Task<int> RunAsync(WorkflowContext context, RaceInput input)
        {
            if (input.RemainingEvent is not null)
                return input.Total + await context.WaitForExternalEventAsync<int>(input.RemainingEvent);
            var first = context.WaitForExternalEventAsync<int>("a");
            var second = context.WaitForExternalEventAsync<int>("b");
            var winner = await Task.WhenAny(first, second);
            context.ContinueAsNew(new RaceInput(winner == first ? "b" : "a", await winner));
            return 0;
        }
    }
    private sealed class DeadlineWorkflow : Workflow<object?, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, object? input)
        {
            try
            {
                await context.CallActivityAsync<string>(nameof(FailingActivity), options: new WorkflowTaskOptions(
                    RetryPolicy: new WorkflowRetryPolicy(3, TimeSpan.FromSeconds(2), retryTimeout: TimeSpan.FromSeconds(2))));
                return "unexpected";
            }
            catch (WorkflowTaskFailedException) { return "expired"; }
        }
    }
    private sealed class FailingActivity(AttemptCounter attempts) : WorkflowActivity<string, string>
    {
        public override Task<string> RunAsync(WorkflowActivityContext context, string input)
        {
            Interlocked.Increment(ref attempts.Count);
            throw new InvalidOperationException("retry");
        }
    }

    private sealed class FailOnceInterceptor(string failure) : Interceptor
    {
        private int _failures;
        private int _connections;
        private int _streamingHistoryInjected;
        public int Failures => Volatile.Read(ref _failures);
        public int Connections => Volatile.Read(ref _connections);

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context, AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            var selected = failure switch
            {
                "workflow" => request is WorkflowResponse,
                "activity" => request is ActivityResponse,
                "history" => request is GetInstanceHistoryRequest,
                _ => false
            };
            if (selected && Interlocked.CompareExchange(ref _failures, 1, 0) == 0)
                throw new RpcException(new Status(StatusCode.Unavailable, "Injected transient delivery failure"));
            return continuation(request, context);
        }

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context, AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            var call = continuation(request, context);
            if (request is not GetWorkItemsRequest)
                return call;
            Interlocked.Increment(ref _connections);
            return new AsyncServerStreamingCall<TResponse>(new Reader<TResponse>(call.ResponseStream, this),
                call.ResponseHeadersAsync, call.GetStatus, call.GetTrailers, call.Dispose);
        }

        private sealed class Reader<T>(IAsyncStreamReader<T> inner, FailOnceInterceptor owner) : IAsyncStreamReader<T>
        {
            public T Current => inner.Current;
            public async Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                var next = await inner.MoveNext(cancellationToken);
                if (next && owner.ShouldInjectHistory() && inner.Current is WorkItem { WorkflowRequest: not null } item &&
                    Interlocked.CompareExchange(ref owner._streamingHistoryInjected, 1, 0) == 0)
                {
                    item.WorkflowRequest.RequiresHistoryStreaming = true;
                }
                return next;
            }
        }

        private bool ShouldInjectHistory() => failure == "history";
    }
}
