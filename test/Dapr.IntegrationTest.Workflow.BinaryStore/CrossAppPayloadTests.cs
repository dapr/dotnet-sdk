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

using System.Text;
using Dapr.Common.Serialization;
using Dapr.Testcontainers;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Containers;
using Dapr.Testcontainers.Harnesses;
using Dapr.Testcontainers.Xunit.Attributes;
using Dapr.Workflow;
using Dapr.Workflow.Payloads;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkflowGrpc = Dapr.DurableTask.Protobuf;

namespace Dapr.IntegrationTest.Workflow.BinaryStore;

public sealed class CrossAppPayloadTests
{
    [MinimumDaprRuntimeTheory("1.19")]
    [InlineData(false, true, null)]
    [InlineData(true, true, null)]
    [InlineData(false, false, null)]
    [InlineData(true, false, null)]
    [InlineData(false, true, "missing")]
    [InlineData(false, true, "too-large")]
    [InlineData(false, true, "corrupt")]
    public async Task ShouldInlineCrossAppInputsUnlessReferencesAreAllowed(
        bool allowReferences, bool referenceInput, string? invalidReference)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        var token = timeout.Token;
        var id = Guid.NewGuid().ToString("N");
        var sourceId = $"payload-source-{id}";
        var destinationId = $"payload-destination-{id}";
        var sourceDirectory = TestDirectoryManager.CreateTestDirectory("payload-source");
        var destinationDirectory = TestDirectoryManager.CreateTestDirectory("payload-destination");
        var calls = new DestinationCalls();
        InMemoryBinaryStoreContainer.Yaml.WriteBinaryStoreYamlToFolder(sourceDirectory);
        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
            needsActorState: true, cancellationToken: token);
        await environment.StartAsync(token);
        var sourceHarness = new DaprHarnessBuilder(sourceDirectory).WithEnvironment(environment)
            .WithOptions(new DaprRuntimeOptions().WithAppId(sourceId)).BuildWorkflow();
        await using var source = await DaprHarnessBuilder.ForHarness(sourceHarness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: runtime => runtime.RegisterWorkflow<CallerWorkflow>(),
                    configureClient: (services, client) =>
                        client.UseGrpcEndpoint(services.GetRequiredService<IConfiguration>()["DAPR_GRPC_ENDPOINT"]!))
                    .WithBinaryStorePayloads(options =>
                    {
                        options.StoreName = Constants.DaprComponentNames.BinaryStoreComponentName;
                        options.WritePreference = PayloadWritePreference.PreferReference;
                        options.OffloadThreshold = 1;
                    });
            }).BuildAndStartAsync();
        var destinationHarness = new DaprHarnessBuilder(destinationDirectory).WithEnvironment(environment)
            .WithOptions(new DaprRuntimeOptions().WithAppId(destinationId)).BuildWorkflow();
        await using var destination = await DaprHarnessBuilder.ForHarness(destinationHarness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddSingleton(calls);
                var workflow = builder.Services.AddDaprWorkflowBuilder(
                    configureRuntime: runtime =>
                    {
                        if (allowReferences)
                        {
                            runtime.RegisterActivity<ReferenceActivity>("measure");
                            runtime.RegisterWorkflow<ReferenceWorkflow>("measure-workflow");
                        }
                        else
                        {
                            runtime.RegisterActivity<DocumentActivity>("measure");
                            runtime.RegisterWorkflow<DocumentWorkflow>("measure-workflow");
                        }
                    },
                    configureClient: (services, client) =>
                        client.UseGrpcEndpoint(services.GetRequiredService<IConfiguration>()["DAPR_GRPC_ENDPOINT"]!));
                if (allowReferences)
                {
                    // These reference-typed consumers inspect metadata only; they never download the object.
                    workflow.WithBinaryStorePayloads(options =>
                        options.StoreName = Constants.DaprComponentNames.BinaryStoreComponentName);
                }
            }).BuildAndStartAsync();
        using var sourceScope = source.CreateScope();
        using var destinationScope = destination.CreateScope();
        var sourceClient = sourceScope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();
        var destinationClient = destinationScope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();
        var serializer = sourceScope.ServiceProvider.GetRequiredService<IDaprSerializer>();
        var document = new Document(new string('x', 128 * 1024));
        var reference = await sourceScope.ServiceProvider.GetRequiredService<IWorkflowPayloadResolver>()
            .StoreAsync(document, id, token);
        reference = invalidReference switch
        {
            "missing" => reference with { Key = "missing-payload" },
            "too-large" => reference with { Size = new BinaryStorePayloadOptions().MaxInMemoryRehydrationSize + 1 },
            "corrupt" => reference with { ContentHash = new string('0', 64) },
            _ => reference
        };
        var childId = $"child-{id}";
        var detachedId = $"detached-{id}";
        await sourceClient.ScheduleNewWorkflowAsync(nameof(CallerWorkflow), id,
            new Request(destinationId, childId, detachedId, reference, allowReferences, referenceInput ? null : document),
            startTime: null, cancellation: token);
        var result = await sourceClient.WaitForWorkflowCompletionAsync(id, true, token);
        if (invalidReference is not null)
        {
            Assert.Equal(WorkflowRuntimeStatus.Failed, result.RuntimeStatus);
            Assert.NotNull(result.FailureDetails);
            var expectedError = invalidReference switch
            {
                "missing" => nameof(PayloadNotFoundException),
                "too-large" => "PayloadRehydrationLimitException",
                "corrupt" => nameof(PayloadIntegrityException),
                _ => throw new InvalidOperationException("Unexpected failure scenario.")
            };
            Assert.EndsWith(expectedError, result.FailureDetails.ErrorType);
            var sourceGrpc = sourceScope.ServiceProvider
                .GetRequiredService<WorkflowGrpc.TaskHubSidecarService.TaskHubSidecarServiceClient>();
            var history = await sourceGrpc.GetInstanceHistoryAsync(
                new WorkflowGrpc.GetInstanceHistoryRequest { InstanceId = id },
                new CallOptions(cancellationToken: token));
            Assert.DoesNotContain(history.Events, e => e.TaskScheduled is not null);
            Assert.Equal(0, calls.Count);
            foreach (var remoteId in new[] { childId, detachedId })
                Assert.False((await destinationClient.GetWorkflowStateAsync(remoteId, false, token)).Exists);
            return;
        }
        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal(reference.Size * 2, result.ReadOutputAs<long>());

        foreach (var remoteId in new[] { childId, detachedId })
        {
            var completion = await destinationClient.WaitForWorkflowCompletionAsync(remoteId, false, token);
            Assert.Equal(WorkflowRuntimeStatus.Completed, completion.RuntimeStatus);
            var grpc = destinationScope.ServiceProvider
                .GetRequiredService<WorkflowGrpc.TaskHubSidecarService.TaskHubSidecarServiceClient>();
            var persisted = await grpc.GetInstanceAsync(
                new WorkflowGrpc.GetInstanceRequest { InstanceId = remoteId, GetInputsAndOutputs = true },
                new CallOptions(cancellationToken: token));
            if (allowReferences && !referenceInput)
            {
                Assert.True(PayloadReference.TryParse(persisted.WorkflowState.Input, out var stored));
                Assert.NotNull(stored);
                Assert.StartsWith($"workflow-payloads/{remoteId}/", stored.Key);
                Assert.Equal(reference.Size, stored.Size);
                Assert.Equal(reference.ContentHash, stored.ContentHash);
                Assert.Equal(document, await sourceScope.ServiceProvider.GetRequiredService<IWorkflowPayloadResolver>()
                    .ResolveAsync<Document>(stored, token));
            }
            else
                Assert.Equal(allowReferences ? reference.ToEnvelope() : serializer.Serialize(document),
                    persisted.WorkflowState.Input);
            Assert.Equal(reference.Size, serializer.Deserialize<long>(persisted.WorkflowState.Output));
        }
        Assert.Equal(3, calls.Count);
    }

    internal sealed record Document(string Content);
    internal sealed record Request(string TargetAppId, string ChildId, string DetachedId,
        PayloadReference<Document> Reference, bool AllowReferences, Document? Value);

    private sealed class DestinationCalls
    {
        private int _count;
        public int Count => Volatile.Read(ref _count);
        public void Record() => Interlocked.Increment(ref _count);
    }

    internal sealed class CallerWorkflow : Workflow<Request, long>
    {
        public override async Task<long> RunAsync(WorkflowContext context, Request input)
        {
            var options = new WorkflowTaskOptions(TargetAppId: input.TargetAppId,
                AllowPayloadReferences: input.AllowReferences);
            object payload = input.Value is { } value ? value : input.Reference;
            var activity = await context.CallActivityAsync<long>("measure", payload, options);
            var child = await context.CallChildWorkflowAsync<long>("measure-workflow", payload,
                new ChildWorkflowTaskOptions(InstanceId: input.ChildId, TargetAppId: input.TargetAppId,
                    AllowPayloadReferences: input.AllowReferences));
            await context.ScheduleNewDetachedWorkflowAsync("measure-workflow", input.DetachedId,
                payload, null, options);
            return activity + child;
        }
    }

    private sealed class DocumentActivity(IDaprSerializer serializer, DestinationCalls calls) : WorkflowActivity<Document, long>
    {
        public override Task<long> RunAsync(WorkflowActivityContext context, Document input)
        {
            calls.Record();
            return Task.FromResult((long)Encoding.UTF8.GetByteCount(serializer.Serialize(input)));
        }
    }

    internal sealed class DocumentWorkflow : Workflow<Document, long>
    {
        public override Task<long> RunAsync(WorkflowContext context, Document input) =>
            context.CallActivityAsync<long>("measure", input);
    }

    private sealed class ReferenceActivity(DestinationCalls calls) : WorkflowActivity<PayloadReference<Document>, long>
    {
        public override Task<long> RunAsync(WorkflowActivityContext context, PayloadReference<Document> input)
        {
            calls.Record();
            return Task.FromResult(input.Size);
        }
    }

    internal sealed class ReferenceWorkflow : Workflow<PayloadReference<Document>, long>
    {
        public override Task<long> RunAsync(WorkflowContext context, PayloadReference<Document> input) =>
            context.CallActivityAsync<long>("measure", input);
    }
}
