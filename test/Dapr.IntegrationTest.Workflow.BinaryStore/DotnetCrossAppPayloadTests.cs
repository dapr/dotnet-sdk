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

using System.Net.Http.Json;
using System.Text.Json;
using Dapr.BinaryStore;
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

public sealed class DotnetCrossAppPayloadTests
{
    [MinimumDaprRuntimeTheory("1.19")]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShouldInlineAtCrossAppBoundariesForDotnetWithoutPayloadSupport(
        bool referenceInput, bool explicitBoundaryOptOut)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        var token = timeout.Token;
        var options = new DaprRuntimeOptions("1.19.0");
        var id = Guid.NewGuid().ToString("N");
        var sourceId = $"reference-source-{id}";
        var destinationId = $"inline-destination-{id}";
        var directory = TestDirectoryManager.CreateTestDirectory("dotnet-cross-app-payloads");
        try
        {
            var sourceDirectory = Path.Combine(directory, "source");
            InMemoryBinaryStoreContainer.Yaml.WriteBinaryStoreYamlToFolder(sourceDirectory);
            await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
                options, needsActorState: true, cancellationToken: token);
            await environment.StartAsync(token);
            var sourceHarness = new DaprHarnessBuilder(sourceDirectory).WithEnvironment(environment)
                .WithOptions((options with { }).WithAppId(sourceId)).BuildWorkflow();
            await using var source = await DaprHarnessBuilder.ForHarness(sourceHarness)
                .ConfigureServices(builder =>
                {
                    builder.Services.AddDaprWorkflowBuilder(
                            configureRuntime: runtime => runtime.RegisterWorkflow<CallerWorkflow>(),
                            configureClient: (services, client) => client.UseGrpcEndpoint(
                                services.GetRequiredService<IConfiguration>()["DAPR_GRPC_ENDPOINT"]!))
                        .WithBinaryStorePayloads(payloads =>
                        {
                            payloads.StoreName = Constants.DaprComponentNames.BinaryStoreComponentName;
                            payloads.WritePreference = PayloadWritePreference.PreferReference;
                            payloads.OffloadThreshold = 1;
                        });
                }).BuildAndStartAsync();

            var calls = new DestinationCalls();
            var destinationHarness = new DaprHarnessBuilder(Path.Combine(directory, "destination"))
                .WithEnvironment(environment).WithOptions((options with { }).WithAppId(destinationId)).BuildWorkflow();
            await using var destination = await DaprHarnessBuilder.ForHarness(destinationHarness)
                .ConfigureServices(builder =>
                {
                    builder.Services.AddSingleton(calls);
                    builder.Services.AddDaprWorkflowBuilder(
                        configureRuntime: runtime =>
                        {
                            runtime.RegisterWorkflow<InlineWorkflow>();
                            runtime.RegisterActivity<InlineActivity>();
                        },
                        configureClient: (services, client) => client.UseGrpcEndpoint(
                            services.GetRequiredService<IConfiguration>()["DAPR_GRPC_ENDPOINT"]!));
                }).BuildAndStartAsync();

            using var sourceScope = source.CreateScope();
            using var destinationScope = destination.CreateScope();
            var sourceServices = sourceScope.ServiceProvider;
            var destinationServices = destinationScope.ServiceProvider;
            Assert.Null(destinationServices.GetService<IWorkflowPayloadResolver>());
            Assert.Null(destinationServices.GetService<IWorkflowPayloadManifest>());
            Assert.Null(destinationServices.GetService<DaprBinaryStoreClient>());

            using var http = new HttpClient();
            var metadata = await Task.WhenAll(new[] { sourceHarness.DaprHttpPort, destinationHarness.DaprHttpPort }
                .Select(port => http.GetFromJsonAsync<JsonElement>($"http://127.0.0.1:{port}/v1.0/metadata", token)));
            Assert.Equal(sourceId, metadata[0].GetProperty("id").GetString());
            Assert.Equal(destinationId, metadata[1].GetProperty("id").GetString());
            Assert.Equal(metadata[0].GetProperty("runtimeVersion").GetString(),
                metadata[1].GetProperty("runtimeVersion").GetString());
            Assert.DoesNotContain(metadata[1].GetProperty("components").EnumerateArray(),
                component =>
                    component.GetProperty("type").GetString()!.StartsWith("binarystore.", StringComparison.Ordinal));

            var sourceClient = sourceServices.GetRequiredService<DaprWorkflowClient>();
            var destinationClient = destinationServices.GetRequiredService<DaprWorkflowClient>();
            var sourceGrpc = sourceServices
                .GetRequiredService<WorkflowGrpc.TaskHubSidecarService.TaskHubSidecarServiceClient>();
            var destinationGrpc = destinationServices
                .GetRequiredService<WorkflowGrpc.TaskHubSidecarService.TaskHubSidecarServiceClient>();
            var serializer = sourceServices.GetRequiredService<IDaprSerializer>();
            var resolver = sourceServices.GetRequiredService<IWorkflowPayloadResolver>();
            var document = new Document(new string('x', 128 * 1024) + "\u00e9\u4e16\u754c\U0001f680\n\"\\",
                long.MaxValue, false, null, ["", "\u00e9", "YWJj"]);
            var reference = await resolver.StoreAsync(document, id, token);
            var request = new Request(destinationId, $"child-{id}", $"detached-{id}", reference,
                referenceInput ? null : document, explicitBoundaryOptOut);
            await sourceClient.ScheduleNewWorkflowAsync(nameof(CallerWorkflow), id, request,
                startTime: null, cancellation: token);
            var completion = await sourceClient.WaitForWorkflowCompletionAsync(id, true, token);
            Assert.True(completion.RuntimeStatus == WorkflowRuntimeStatus.Completed,
                completion.FailureDetails?.ToString());
            var expected = document with { Content = document.Content + "|dotnet-inline" };
            Assert.Equivalent(new Result(expected, expected), completion.ReadOutputAs<Result>());

            // Offloading remains enabled locally; only the remote call boundaries are inlined.
            var persisted = await sourceGrpc.GetInstanceAsync(
                new WorkflowGrpc.GetInstanceRequest { InstanceId = id, GetInputsAndOutputs = true },
                new CallOptions(cancellationToken: token));
            Assert.True(PayloadReference.TryParse(persisted.WorkflowState.Input, out _));
            Assert.True(PayloadReference.TryParse(persisted.WorkflowState.Output, out var outputReference));
            Assert.NotNull(outputReference);
            Assert.Equivalent(new Result(expected, expected),
                await resolver.ResolveAsync<Result>(outputReference, token));
            var history = await sourceGrpc.GetInstanceHistoryAsync(
                new WorkflowGrpc.GetInstanceHistoryRequest { InstanceId = id },
                new CallOptions(cancellationToken: token));
            AssertInlineActivityHistory(history.Events, document, expected, serializer);
            AssertInline(Assert.Single(history.Events, e => e.ChildWorkflowInstanceCompleted is not null)
                .ChildWorkflowInstanceCompleted.Result, expected, serializer);

            foreach (var remoteId in new[] { request.ChildId, request.DetachedId })
            {
                var remote = await destinationClient.WaitForWorkflowCompletionAsync(remoteId, true, token);
                Assert.True(remote.RuntimeStatus == WorkflowRuntimeStatus.Completed, remote.FailureDetails?.ToString());
                Assert.Equivalent(document, remote.ReadInputAs<Document>());
                Assert.Equivalent(expected, remote.ReadOutputAs<Document>());
                var remoteState = await destinationGrpc.GetInstanceAsync(
                    new WorkflowGrpc.GetInstanceRequest { InstanceId = remoteId, GetInputsAndOutputs = true },
                    new CallOptions(cancellationToken: token));
                AssertInline(remoteState.WorkflowState.Input, document, serializer);
                AssertInline(remoteState.WorkflowState.Output, expected, serializer);
                var remoteHistory = await destinationGrpc.GetInstanceHistoryAsync(
                    new WorkflowGrpc.GetInstanceHistoryRequest { InstanceId = remoteId },
                    new CallOptions(cancellationToken: token));
                AssertInlineActivityHistory(remoteHistory.Events, document, expected, serializer);
            }

            Assert.Equal(3, calls.Count);
        }
        finally
        {
            TestDirectoryManager.CleanUpDirectory(directory);
        }
    }

    private static void AssertInlineActivityHistory(IEnumerable<WorkflowGrpc.HistoryEvent> events,
        Document input, Document output, IDaprSerializer serializer)
    {
        AssertInline(Assert.Single(events, e => e.TaskScheduled is not null).TaskScheduled.Input, input, serializer);
        AssertInline(Assert.Single(events, e => e.TaskCompleted is not null).TaskCompleted.Result, output, serializer);
    }

    private static void AssertInline(string wire, Document expected, IDaprSerializer serializer)
    {
        Assert.False(PayloadReference.TryParse(wire, out _), "Expected inline data at the unconfigured .NET boundary.");
        Assert.Equal(serializer.Serialize(expected), wire);
    }

    internal sealed record Document(string Content, long Sequence, bool Enabled, string? Optional, string[] Items);

    internal sealed record Request(
        string TargetAppId,
        string ChildId,
        string DetachedId,
        PayloadReference<Document> Reference,
        Document? Value,
        bool ExplicitBoundaryOptOut);

    internal sealed record Result(Document Activity, Document Child);

    private sealed class DestinationCalls
    {
        private int _count;
        public int Count => Volatile.Read(ref _count);
        public void Record() => Interlocked.Increment(ref _count);
    }

    internal sealed class CallerWorkflow : Workflow<Request, Result>
    {
        public override async Task<Result> RunAsync(WorkflowContext context, Request input)
        {
            object payload = input.Value is { } value ? value : input.Reference;
            var options = input.ExplicitBoundaryOptOut
                ? new WorkflowTaskOptions(TargetAppId: input.TargetAppId, AllowPayloadReferences: false)
                : new WorkflowTaskOptions(TargetAppId: input.TargetAppId);
            var childOptions = input.ExplicitBoundaryOptOut
                ? new ChildWorkflowTaskOptions(InstanceId: input.ChildId, TargetAppId: input.TargetAppId,
                    AllowPayloadReferences: false)
                : new ChildWorkflowTaskOptions(InstanceId: input.ChildId, TargetAppId: input.TargetAppId);
            var activity = await context.CallActivityAsync<Document>(nameof(InlineActivity), payload, options);
            var child = await context.CallChildWorkflowAsync<Document>(nameof(InlineWorkflow), payload, childOptions);
            await context.ScheduleNewDetachedWorkflowAsync(nameof(InlineWorkflow), input.DetachedId, payload, null,
                options);
            return new Result(activity, child);
        }
    }

    internal sealed class InlineWorkflow : Workflow<Document, Document>
    {
        public override Task<Document> RunAsync(WorkflowContext context, Document input)
        {
            Assert.False(context.HasPayloadResolver());
            return context.CallActivityAsync<Document>(nameof(InlineActivity), input);
        }
    }

    private sealed class InlineActivity(DestinationCalls calls) : WorkflowActivity<Document, Document>
    {
        public override Task<Document> RunAsync(WorkflowActivityContext context, Document input)
        {
            calls.Record();
            return Task.FromResult(input with { Content = input.Content + "|dotnet-inline" });
        }
    }
}
