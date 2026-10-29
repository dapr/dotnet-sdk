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
using System.Security.Cryptography;
using System.Text.Json;
using Dapr.BinaryStore;
using Dapr.Testcontainers;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Containers;
using Dapr.Testcontainers.Containers.Dapr;
using Dapr.Testcontainers.Harnesses;
using Dapr.Testcontainers.Xunit.Attributes;
using Dapr.Workflow;
using Dapr.Workflow.Payloads;
using DotNet.Testcontainers.Builders;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkflowGrpc = Dapr.DurableTask.Protobuf;

namespace Dapr.IntegrationTest.Workflow.BinaryStore;

public sealed class JavaScriptCrossAppPayloadTests
{
    private const string StoreName = Constants.DaprComponentNames.BinaryStoreComponentName;

    [MinimumDaprRuntimeTheory("1.19")]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShouldResolveReferencesAcrossSdkActivityChildAndDetachedWorkflows(
        bool javascriptParent, bool explicitReference)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        var token = timeout.Token;
        var options = new DaprRuntimeOptions("1.19.0");
        var id = Guid.NewGuid().ToString("N");
        var dotnetId = $"dotnet-{id}";
        var javascriptId = $"javascript-{id}";
        var directory = TestDirectoryManager.CreateTestDirectory("cross-sdk-payloads");
        try
        {
            await using var image = new ImageFromDockerfileBuilder()
                .WithDockerfileDirectory(Path.Combine(AppContext.BaseDirectory, "JavaScript"))
                .WithName($"dapr-payload-interop-{id}")
                .Build();
            await image.CreateAsync(token);
            await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
                options, needsActorState: true, cancellationToken: token);
            await environment.StartAsync(token);

            // In-memory stores are sidecar-local. Both SDK resolvers use this same store sidecar,
            // rather than accidentally creating two unrelated stores with the same component name.
            var storeDirectory = Path.Combine(directory, "store");
            InMemoryBinaryStoreContainer.Yaml.WriteBinaryStoreYamlToFolder(storeDirectory);
            await using var storeSidecar = new DaprdContainer($"store-{id}", storeDirectory,
                options with { AppPort = 0 }, environment.Network);
            await storeSidecar.StartAsync(token);

            var javascriptDirectory = Path.Combine(directory, "javascript");
            RedisContainer.Yaml.WriteStateStoreYamlToFolder(javascriptDirectory,
                redisHost: $"{environment.RedisContainer!.NetworkAlias}:{RedisContainer.ContainerPort}");
            await using var javascriptSidecar = new DaprdContainer(javascriptId, javascriptDirectory,
                options with { AppPort = 0 }, environment.Network,
                new HostPortPair(environment.PlacementAlias, DaprPlacementContainer.InternalPort),
                new HostPortPair(environment.SchedulerAlias, DaprSchedulerContainer.InternalPort));
            await javascriptSidecar.StartAsync(token);

            var dotnetHarness = new DaprHarnessBuilder(Path.Combine(directory, "dotnet"))
                .WithEnvironment(environment).WithOptions((options with { }).WithAppId(dotnetId)).BuildWorkflow();
            await using var dotnet = await DaprHarnessBuilder.ForHarness(dotnetHarness)
                .ConfigureServices(builder =>
                {
                    builder.Services.AddDaprWorkflowBuilder(
                            configureRuntime: runtime =>
                            {
                                runtime.RegisterWorkflow<ParentWorkflow>("parent");
                                runtime.RegisterWorkflow<AutomaticWorkflow>("automatic");
                                runtime.RegisterWorkflow<ReferenceWorkflow>("reference");
                                runtime.RegisterActivity<AutomaticActivity>("automatic");
                                runtime.RegisterActivity<ReferenceActivity>("reference");
                            },
                            configureClient: (services, client) => client.UseGrpcEndpoint(
                                services.GetRequiredService<IConfiguration>()["DAPR_GRPC_ENDPOINT"]!))
                        .WithBinaryStorePayloads(payloads =>
                        {
                            payloads.StoreName = StoreName;
                            payloads.WritePreference = PayloadWritePreference.PreferReference;
                            payloads.OffloadThreshold = 1;
                        });
                    builder.Services.AddSingleton<DaprBinaryStoreClient>(_ =>
                        new DaprBinaryStoreClientBuilder()
                            .UseGrpcEndpoint($"http://127.0.0.1:{storeSidecar.GrpcPort}").Build());
                }).BuildAndStartAsync();

            await using var javascript = new ContainerBuilder()
                .WithImage(image).WithNetwork(environment.Network)
                .WithEnvironment("DAPR_HOST", javascriptSidecar.NetworkAlias)
                .WithEnvironment("STORE_HOST", storeSidecar.NetworkAlias)
                .WithEnvironment("STORE_NAME", StoreName)
                .WithPortBinding(8080, true)
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(request => request.ForPort(8080).ForPath("/health")))
                .Build();
            await javascript.StartAsync(token);

            using var http = new HttpClient();
            var metadata = await Task.WhenAll(new[] { dotnetHarness.DaprHttpPort, javascriptSidecar.HttpPort }
                .Select(port => http.GetFromJsonAsync<JsonElement>($"http://127.0.0.1:{port}/v1.0/metadata", token)));
            Assert.Equal(dotnetId, metadata[0].GetProperty("id").GetString());
            Assert.Equal(javascriptId, metadata[1].GetProperty("id").GetString());
            Assert.Equal(metadata[0].GetProperty("runtimeVersion").GetString(),
                metadata[1].GetProperty("runtimeVersion").GetString());

            using var scope = dotnet.CreateScope();
            var services = scope.ServiceProvider;
            var resolver = services.GetRequiredService<IWorkflowPayloadResolver>();
            var store = services.GetRequiredService<DaprBinaryStoreClient>();
            var document = new Document(new string('x', 128 * 1024) + "\u00e9\u4e16\u754c\U0001f680\n\"\\",
                9_007_199_254_740_991, false, null, ["", "\u00e9", "YWJj"]);
            var reference = await resolver.StoreAsync(document, id, token);
            var request = new Request(javascriptParent ? dotnetId : javascriptId,
                $"child-{id}", $"detached-{id}", explicitReference, reference);
            var parentPort = javascriptParent ? javascriptSidecar.GrpcPort : dotnetHarness.DaprGrpcPort;
            var remotePort = javascriptParent ? dotnetHarness.DaprGrpcPort : javascriptSidecar.GrpcPort;
            if (javascriptParent)
            {
                using var envelope = JsonDocument.Parse(reference.ToEnvelope());
                using var response = await http.PostAsJsonAsync(
                    $"http://{javascript.Hostname}:{javascript.GetMappedPublicPort(8080)}/start",
                    new
                    {
                        instanceId = id,
                        input = new
                        {
                            request.TargetAppId,
                            request.ChildId,
                            request.DetachedId,
                            request.ExplicitReference,
                            reference = envelope.RootElement
                        }
                    }, token);
                Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(token));
            }
            else
            {
                await services.GetRequiredService<DaprWorkflowClient>().ScheduleNewWorkflowAsync(
                    "parent", id, request, startTime: null, cancellation: token);
            }

            using var parentChannel = GrpcChannel.ForAddress($"http://127.0.0.1:{parentPort}");
            using var remoteChannel = GrpcChannel.ForAddress($"http://127.0.0.1:{remotePort}");
            var parentGrpc = new WorkflowGrpc.TaskHubSidecarService.TaskHubSidecarServiceClient(parentChannel);
            var remoteGrpc = new WorkflowGrpc.TaskHubSidecarService.TaskHubSidecarServiceClient(remoteChannel);
            var expected = document with
            {
                Content = document.Content + (javascriptParent ? "|dotnet" : "|javascript")
            };
            var completion = await WaitAsync(parentGrpc, id, token);
            var outputReference = await AssertReferenceAsync(completion.Output, store, token);
            var result = await resolver.ResolveAsync<Result>(outputReference, token);
            Assert.Equivalent(expected, result.Activity);
            Assert.Equivalent(expected, result.Child);

            var history = await parentGrpc.GetInstanceHistoryAsync(
                new WorkflowGrpc.GetInstanceHistoryRequest { InstanceId = id },
                new CallOptions(cancellationToken: token));
            var scheduled = Assert.Single(history.Events, e => e.TaskScheduled is not null).TaskScheduled;
            var activityInput = await AssertReferenceAsync(scheduled.Input, store, token);
            Assert.Equivalent(document, await resolver.ResolveAsync<Document>(activityInput, token));
            if (explicitReference)
                Assert.Equal(reference.ToEnvelope(), activityInput.ToEnvelope());
            var activityOutput = Assert.Single(history.Events, e => e.TaskCompleted is not null).TaskCompleted.Result;
            var childOutput = Assert.Single(history.Events, e => e.ChildWorkflowInstanceCompleted is not null)
                .ChildWorkflowInstanceCompleted.Result;
            foreach (var wire in new[] { activityOutput, childOutput })
            {
                var returned = await AssertReferenceAsync(wire, store, token);
                Assert.Equivalent(expected, await resolver.ResolveAsync<Document>(returned, token));
            }

            foreach (var remoteId in new[] { request.ChildId, request.DetachedId })
            {
                var remote = await WaitAsync(remoteGrpc, remoteId, token);
                var inputReference = await AssertReferenceAsync(remote.Input, store, token);
                Assert.Equivalent(document, await resolver.ResolveAsync<Document>(inputReference, token));
                var returned = await AssertReferenceAsync(remote.Output, store, token);
                Assert.Equivalent(expected, await resolver.ResolveAsync<Document>(returned, token));
                if (explicitReference)
                    Assert.Equal(reference.ToEnvelope(), inputReference.ToEnvelope());
            }
        }
        finally
        {
            TestDirectoryManager.CleanUpDirectory(directory);
        }
    }

    private static async Task<WorkflowGrpc.WorkflowState> WaitAsync(
        WorkflowGrpc.TaskHubSidecarService.TaskHubSidecarServiceClient client, string id, CancellationToken token)
    {
        var response = await client.WaitForInstanceCompletionAsync(
            new WorkflowGrpc.GetInstanceRequest { InstanceId = id, GetInputsAndOutputs = true },
            new CallOptions(cancellationToken: token));
        Assert.True(response.Exists);
        Assert.True(response.WorkflowState.WorkflowStatus == WorkflowGrpc.OrchestrationStatus.Completed,
            response.WorkflowState.FailureDetails?.ToString() ?? response.WorkflowState.ToString());
        return response.WorkflowState;
    }

    private static async Task<PayloadReference> AssertReferenceAsync(
        string wire, DaprBinaryStoreClient store, CancellationToken token)
    {
        Assert.True(PayloadReference.TryParse(wire, out var reference),
            "Expected a persisted data reference, not inline JSON.");
        Assert.NotNull(reference);
        Assert.Equal(StoreName, reference.StoreName);
        using var bytes = new MemoryStream();
        await foreach (var chunk in store.GetBinaryFileChunksAsync(reference.StoreName, reference.Key, token))
            await bytes.WriteAsync(chunk, token);
        Assert.Equal(reference.Size, bytes.Length);
        Assert.Equal(reference.ContentHash, Convert.ToHexString(SHA256.HashData(bytes.ToArray())).ToLowerInvariant());
        return reference;
    }

    internal sealed record Document(string Content, long Sequence, bool Enabled, string? Optional, string[] Items);

    internal sealed record Request(
        string TargetAppId,
        string ChildId,
        string DetachedId,
        bool ExplicitReference,
        PayloadReference<Document> Reference);

    internal sealed record Result(Document Activity, Document Child);

    internal sealed class ParentWorkflow : Workflow<Request, Result>
    {
        public override async Task<Result> RunAsync(WorkflowContext context, Request input)
        {
            object payload = input.ExplicitReference
                ? input.Reference
                : await context.ResolvePayloadAsync(input.Reference);
            var name = input.ExplicitReference ? "reference" : "automatic";
            var options = new WorkflowTaskOptions(TargetAppId: input.TargetAppId, AllowPayloadReferences: true);
            var activity = await context.CallActivityAsync<PayloadReference<Document>>(name, payload, options);
            var child = await context.CallChildWorkflowAsync<PayloadReference<Document>>(name, payload,
                new ChildWorkflowTaskOptions(InstanceId: input.ChildId, TargetAppId: input.TargetAppId,
                    AllowPayloadReferences: true));
            await context.ScheduleNewDetachedWorkflowAsync(name, input.DetachedId, payload, null, options);
            return new Result(await context.ResolvePayloadAsync(activity), await context.ResolvePayloadAsync(child));
        }
    }

    internal sealed class AutomaticWorkflow : Workflow<Document, Document>
    {
        public override Task<Document> RunAsync(WorkflowContext context, Document input) =>
            context.CallActivityAsync<Document>("automatic", input);
    }

    internal sealed class ReferenceWorkflow : Workflow<PayloadReference<Document>, PayloadReference<Document>>
    {
        public override async Task<PayloadReference<Document>> RunAsync(WorkflowContext context,
            PayloadReference<Document> input)
        {
            Assert.Equal(9_007_199_254_740_991, (await context.ResolvePayloadAsync(input)).Sequence);
            return await context.CallActivityAsync<PayloadReference<Document>>("reference", input);
        }
    }

    private sealed class AutomaticActivity : WorkflowActivity<Document, Document>
    {
        public override Task<Document> RunAsync(WorkflowActivityContext context, Document input) =>
            Task.FromResult(input with { Content = input.Content + "|dotnet" });
    }

    private sealed class ReferenceActivity(IWorkflowPayloadResolver resolver)
        : WorkflowActivity<PayloadReference<Document>, PayloadReference<Document>>
    {
        public override async Task<PayloadReference<Document>> RunAsync(
            WorkflowActivityContext context, PayloadReference<Document> input)
        {
            var document = await resolver.ResolveAsync<Document>(input);
            return await resolver.StoreAsync(document with { Content = document.Content + "|dotnet" },
                context.InstanceId);
        }
    }
}
