// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Dapr.Common.Serialization;
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Client;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Dapr.Workflow.Test;

public sealed class WorkflowClientReliabilityTests
{
    private static Mock<TaskHubSidecarService.TaskHubSidecarServiceClient> Client() =>
        new(new Mock<CallInvoker>().Object);
    private static AsyncUnaryCall<T> Unary<T>(T value) => new(
        Task.FromResult(value), Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => [], () => { });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegisteredSerializer_AppliesWithoutPayloadFeature(bool customSerializer)
    {
        var grpc = Client();
        CreateInstanceRequest? sent = null;
        grpc.Setup(c => c.StartInstanceAsync(It.IsAny<CreateInstanceRequest>(), It.IsAny<CallOptions>()))
            .Callback<CreateInstanceRequest, CallOptions>((request, _) => sent = request)
            .Returns(Unary(new CreateInstanceResponse { InstanceId = "instance" }));
        var services = new ServiceCollection().AddLogging();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        var builder = services.AddDaprWorkflow();
        if (customSerializer)
            builder.WithSerializer(new JsonDaprSerializer(options));
        else
            builder.WithJsonSerializer(options);
        services.AddSingleton(grpc.Object);
        using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<DaprWorkflowClient>().ScheduleNewWorkflowAsync(
            "workflow", "instance", new Payment { PaymentAmount = 42 }, startTime: null,
            cancellation: TestContext.Current.CancellationToken);
        Assert.Equal(42, provider.GetRequiredService<IDaprSerializer>().Deserialize<Payment>(sent!.Input)!.PaymentAmount);
        Assert.Contains("payment_amount", sent.Input);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CompletionWait_ReturnsOriginalSnapshotWithoutRefetch(bool includePayloads)
    {
        var grpc = Client();
        grpc.Setup(c => c.WaitForInstanceCompletionAsync(
                It.Is<GetInstanceRequest>(r => r.GetInputsAndOutputs == includePayloads), It.IsAny<CallOptions>()))
            .Returns(Unary(new GetInstanceResponse
            {
                Exists = true,
                WorkflowState = new()
                {
                    InstanceId = "reused", Name = "original", WorkflowStatus = OrchestrationStatus.Completed,
                    Output = includePayloads ? "\"original output\"" : ""
                }
            }));
        grpc.Setup(c => c.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()))
            .Returns(Unary(new GetInstanceResponse
            {
                Exists = true,
                WorkflowState = new() { InstanceId = "reused", Name = "replacement", WorkflowStatus = OrchestrationStatus.Running }
            }));
        var client = new DaprWorkflowClient(new WorkflowGrpcClient(grpc.Object,
            NullLogger<WorkflowGrpcClient>.Instance, new JsonDaprSerializer()));
        var result = await client.WaitForWorkflowCompletionAsync("reused", includePayloads, TestContext.Current.CancellationToken);
        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal("original", result.WorkflowName);
        if (includePayloads)
            Assert.Equal("original output", result.ReadOutputAs<string>());
        grpc.Verify(c => c.GetInstanceAsync(It.IsAny<GetInstanceRequest>(), It.IsAny<CallOptions>()), Times.Never);
    }

    public sealed class Payment
    {
        public int PaymentAmount { get; set; }
    }
}
