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

using System.Reflection;
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Serialization;
using Dapr.Workflow.Worker;
using Dapr.Workflow.Worker.Grpc;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Dapr.Workflow.Test.Worker;

public class WorkflowWorkerTests
{
    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenGrpcClientIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WorkflowWorker(null!, Mock.Of<IWorkflowsFactory>(), Mock.Of<ILoggerFactory>(), Mock.Of<IWorkflowSerializer>(),
                new ServiceCollection().BuildServiceProvider(), new WorkflowRuntimeOptions()));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenWorkflowsFactoryIsNull()
    {
        var grpcClient = CreateGrpcClientMock().Object;

        Assert.Throws<ArgumentNullException>(() =>
            new WorkflowWorker(grpcClient, null!, Mock.Of<ILoggerFactory>(), Mock.Of<IWorkflowSerializer>(),
                new ServiceCollection().BuildServiceProvider(), new WorkflowRuntimeOptions()));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenLoggerFactoryIsNull()
    {
        var grpcClient = CreateGrpcClientMock().Object;

        Assert.Throws<ArgumentNullException>(() =>
            new WorkflowWorker(grpcClient, Mock.Of<IWorkflowsFactory>(), null!, Mock.Of<IWorkflowSerializer>(),
                new ServiceCollection().BuildServiceProvider(), new WorkflowRuntimeOptions()));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenSerializerIsNull()
    {
        var grpcClient = CreateGrpcClientMock().Object;

        Assert.Throws<ArgumentNullException>(() =>
            new WorkflowWorker(grpcClient, Mock.Of<IWorkflowsFactory>(), Mock.Of<ILoggerFactory>(), null!,
                new ServiceCollection().BuildServiceProvider(), new WorkflowRuntimeOptions()));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenServiceProviderIsNull()
    {
        var grpcClient = CreateGrpcClientMock().Object;

        Assert.Throws<ArgumentNullException>(() =>
            new WorkflowWorker(grpcClient, Mock.Of<IWorkflowsFactory>(), Mock.Of<ILoggerFactory>(), Mock.Of<IWorkflowSerializer>(),
                null!, new WorkflowRuntimeOptions()));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenOptionsIsNull()
    {
        var grpcClient = CreateGrpcClientMock().Object;

        Assert.Throws<ArgumentNullException>(() =>
            new WorkflowWorker(grpcClient, Mock.Of<IWorkflowsFactory>(), Mock.Of<ILoggerFactory>(), Mock.Of<IWorkflowSerializer>(),
                new ServiceCollection().BuildServiceProvider(), null!));
    }

    [Fact]
    public async Task StopAsync_ShouldNotThrow_WhenProtocolHandlerWasNeverCreated()
    {
        var grpcClient = CreateGrpcClientMock().Object;
        var worker = new WorkflowWorker(
            grpcClient,
            Mock.Of<IWorkflowsFactory>(),
            NullLoggerFactory.Instance,
            Mock.Of<IWorkflowSerializer>(),
            new ServiceCollection().BuildServiceProvider(),
            new WorkflowRuntimeOptions());

        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_ShouldDisposeProtocolHandler_WhenPresent()
    {
        var grpcClient = CreateGrpcClientMock().Object;
        var worker = new WorkflowWorker(
            grpcClient,
            Mock.Of<IWorkflowsFactory>(),
            NullLoggerFactory.Instance,
            Mock.Of<IWorkflowSerializer>(),
            new ServiceCollection().BuildServiceProvider(),
            new WorkflowRuntimeOptions());

        var protocolHandler = new GrpcProtocolHandler(CreateGrpcClientMock().Object, NullLoggerFactory.Instance, 1, 1);

        var field = typeof(WorkflowWorker).GetField("_protocolHandler", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(worker, protocolHandler);

        await worker.StopAsync(CancellationToken.None);
    }

    private static Mock<TaskHubSidecarService.TaskHubSidecarServiceClient> CreateGrpcClientMock()
    {
        var callInvoker = new Mock<CallInvoker>(MockBehavior.Loose);

        return new Mock<TaskHubSidecarService.TaskHubSidecarServiceClient>(callInvoker.Object);
    }
}
