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

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.BinaryStore;
using Dapr.Common.Serialization;
using Dapr.Testcontainers;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Common.Testing;
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

public sealed class WorkflowBinaryStoreIntegrationTests
{
    private const string StoreName = Constants.DaprComponentNames.BinaryStoreComponentName;
    private const string KeyPrefix = "workflow-integration";

    [MinimumDaprRuntimeTheory("1.19")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldKeepLargePayloadsInlineWithoutOptIn(bool binaryStoreAvailable)
    {
        var binaryStoreActivations = 0;
        var (environment, app) = await StartAsync(
            enablePayloadSupport: false,
            configureBinaryStore: binaryStoreAvailable,
            configureServices: services =>
            {
                if (binaryStoreAvailable)
                {
                    services.AddSingleton<DaprBinaryStoreClient>(_ =>
                    {
                        Interlocked.Increment(ref binaryStoreActivations);
                        throw new InvalidOperationException("Workflow payload support must not activate the binary store client without opt-in.");
                    });
                }
            });
        await using var environmentLifetime = environment;
        await using var appLifetime = app;
        using var scope = app.CreateScope();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(1));
        var services = scope.ServiceProvider;
        Assert.Null(services.GetService<IWorkflowPayloadResolver>());
        Assert.Null(services.GetService<IWorkflowPayloadManifest>());
        Assert.Null(services.GetService<WorkflowPayloadCleaner>());
        Assert.Equal(binaryStoreAvailable,
            services.GetRequiredService<IServiceProviderIsService>().IsService(typeof(DaprBinaryStoreClient)));
        var client = services.GetRequiredService<DaprWorkflowClient>();
        var serializer = services.GetRequiredService<IDaprSerializer>();
        var instanceId = Guid.NewGuid().ToString("N");
        var payload = new PayloadDocument("input", new string('x', 128 * 1024));
        Assert.True(Encoding.UTF8.GetByteCount(serializer.Serialize(payload)) >
                    new BinaryStorePayloadOptions().OffloadThreshold);

        await client.ScheduleNewWorkflowAsync(nameof(UnconfiguredPayloadWorkflow), instanceId, payload,
            startTime: null, cancellation: timeout.Token);
        await client.RaiseEventAsync(instanceId, "payload", payload with { Name = "event" }, timeout.Token);
        var result = await client.WaitForWorkflowCompletionAsync(instanceId, true, timeout.Token);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var expected = payload with { Name = "continued" };
        Assert.Equal(expected, result.ReadInputAs<PayloadDocument>());
        Assert.Equal(expected, result.ReadOutputAs<PayloadDocument>());
        Assert.Equal(expected, result.ReadCustomStatusAs<PayloadDocument>());

        // Inspect the sidecar directly so SDK rehydration cannot mask accidental offloading.
        var grpc = services.GetRequiredService<WorkflowGrpc.TaskHubSidecarService.TaskHubSidecarServiceClient>();
        var persisted = await grpc.GetInstanceAsync(
            new WorkflowGrpc.GetInstanceRequest { InstanceId = instanceId, GetInputsAndOutputs = true },
            new CallOptions(cancellationToken: timeout.Token));
        Assert.True(persisted.Exists);
        var serialized = serializer.Serialize(expected);
        Assert.Equal(serialized, persisted.WorkflowState.Input);
        Assert.Equal(serialized, persisted.WorkflowState.Output);
        Assert.Equal(serialized, persisted.WorkflowState.CustomStatus);
        Assert.Equal(0, Volatile.Read(ref binaryStoreActivations));
    }

    [MinimumDaprRuntimeFact("1.19")]
    public async Task ShouldNotInterpretReferenceEnvelopesWithoutOptIn()
    {
        var (environment, app) = await StartAsync(enablePayloadSupport: false, configureBinaryStore: false);
        await using var environmentLifetime = environment;
        await using var appLifetime = app;
        using var scope = app.CreateScope();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(1));
        var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();
        var instanceId = Guid.NewGuid().ToString("N");
        var reference = new PayloadReference("unconfigured-store", "must-not-be-read", 128 * 1024, new string('0', 64));
        using var document = JsonDocument.Parse(reference.ToEnvelope());
        var payload = document.RootElement;

        await client.ScheduleNewWorkflowAsync(nameof(UnconfiguredEnvelopeWorkflow), instanceId, payload,
            startTime: null, cancellation: timeout.Token);
        var result = await client.WaitForWorkflowCompletionAsync(instanceId, true, timeout.Token);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal(reference.ToEnvelope(), result.ReadInputAs<JsonElement>().GetRawText());
        Assert.Equal(reference.ToEnvelope(), result.ReadOutputAs<JsonElement>().GetRawText());
        Assert.Equal(reference.ToEnvelope(), result.ReadCustomStatusAs<JsonElement>().GetRawText());
    }

    [MinimumDaprRuntimeFact("1.19")]
    public async Task ShouldAutomaticallyOffloadAndReplayAllWorkflowBoundaries()
    {
        var (environment, app) = await StartAsync();
        await using var environmentLifetime = environment;
        await using var appLifetime = app;
        using var scope = app.CreateScope();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(1));
        var client = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();
        var instanceId = Guid.NewGuid().ToString("N");
        var payload = new PayloadDocument("input", new string('x', 128 * 1024));

        await client.ScheduleNewWorkflowAsync(nameof(AutomaticPayloadWorkflow), instanceId, payload,
            startTime: null, cancellation: timeout.Token);
        await client.RaiseEventAsync(instanceId, "payload", payload with { Name = "event" }, timeout.Token);
        var result = await client.WaitForWorkflowCompletionAsync(instanceId, true, timeout.Token);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        var expected = payload with { Name = "continued" };
        Assert.Equal(expected, result.ReadInputAs<PayloadDocument>());
        Assert.Equal(expected, result.ReadOutputAs<PayloadDocument>());
        Assert.Equal(expected, result.ReadCustomStatusAs<PayloadDocument>());
        var reference = result.ReadOutputAs<PayloadReference<PayloadDocument>>();
        Assert.NotNull(reference);
        Assert.StartsWith($"{KeyPrefix}/{instanceId}/", reference.Key, StringComparison.Ordinal);
        var manifest = scope.ServiceProvider.GetRequiredService<IWorkflowPayloadManifest>();
        Assert.True((await manifest.ListAsync(instanceId, timeout.Token)).Count >= 4);
    }

    [MinimumDaprRuntimeFact("1.19")]
    public async Task ShouldOffloadStreamAndResolvePayloadAcrossWorkflowActivity()
    {
        var (environment, app) = await StartAsync();
        await using var environmentLifetime = environment;
        await using var appLifetime = app;

        using var scope = app.CreateScope();
        var cancellationToken = TestContext.Current.CancellationToken;
        var instanceId = Guid.NewGuid().ToString("N");
        var payload = new PayloadDocument("integration", new string('x', 128 * 1024));
        var resolver = scope.ServiceProvider.GetRequiredService<IWorkflowPayloadResolver>();
        var reference = await resolver.StoreAsync(payload, instanceId, cancellationToken);
        Assert.Equal(StoreName, reference.StoreName);
        Assert.True(reference.Size > payload.Content.Length);
        Assert.StartsWith($"{KeyPrefix}/{instanceId}/", reference.Key, StringComparison.Ordinal);

        var repeatedReference = await resolver.StoreAsync(payload, instanceId, cancellationToken);
        Assert.Equal(reference, repeatedReference);

        var binaryStore = scope.ServiceProvider.GetRequiredService<DaprBinaryStoreClient>();
        var serialized =
            Encoding.UTF8.GetBytes(scope.ServiceProvider.GetRequiredService<IDaprSerializer>().Serialize(payload));
        Assert.Equal(serialized.LongLength, reference.Size);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(serialized)).ToLowerInvariant(), reference.ContentHash);
        Assert.Equal(serialized,
            await binaryStore.GetBinaryFileBytesAsync(reference.StoreName, reference.Key, cancellationToken));

        var hydrated = await resolver.ResolveAsync<PayloadDocument>(reference, cancellationToken);
        Assert.Equal(payload, hydrated);

        await using (var stream = await resolver.OpenAsync(reference, cancellationToken))
        using (var copy = new MemoryStream())
        {
            await stream.CopyToAsync(copy, cancellationToken);
            Assert.Equal(serialized, copy.ToArray());
        }

        var typedReference = reference.AsTyped<PayloadDocument>();
        var workflowClient = scope.ServiceProvider.GetRequiredService<DaprWorkflowClient>();
        await workflowClient.ScheduleNewWorkflowAsync(nameof(ResolvePayloadWorkflow), instanceId, typedReference,
            startTime: null, cancellation: cancellationToken);
        var result = await workflowClient.WaitForWorkflowCompletionAsync(instanceId, true, cancellationToken);

        Assert.Equal(WorkflowRuntimeStatus.Completed, result.RuntimeStatus);
        Assert.Equal($"{payload.Name}:{payload.Content.Length}", result.ReadOutputAs<string>());

        var cleaner = scope.ServiceProvider.GetRequiredService<WorkflowPayloadCleaner>();
        Assert.Equal(1, await cleaner.PurgeAsync(instanceId, cancellationToken));
        var missingFile = await Assert.ThrowsAsync<DaprException>(() =>
            binaryStore.GetBinaryFileBytesAsync(reference.StoreName, reference.Key, cancellationToken));
        Assert.Equal(StatusCode.NotFound, Assert.IsType<RpcException>(missingFile.InnerException).StatusCode);
    }

    private static async Task<(DaprTestEnvironment Environment, DaprTestApplication App)> StartAsync(
        bool enablePayloadSupport = true,
        bool configureBinaryStore = true,
        Action<IServiceCollection>? configureServices = null)
    {
        var componentsDirectory = TestDirectoryManager.CreateTestDirectory("workflow-binary-store-components");
        if (configureBinaryStore)
            InMemoryBinaryStoreContainer.Yaml.WriteBinaryStoreYamlToFolder(componentsDirectory);

        var options = new DaprRuntimeOptions();
        var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
            options,
            needsActorState: true,
            cancellationToken: TestContext.Current.CancellationToken);
        var harness = new DaprHarnessBuilder(componentsDirectory)
            .WithOptions(options)
            .WithEnvironment(environment)
            .BuildWorkflow();

        try
        {
            await environment.StartAsync(TestContext.Current.CancellationToken);
            var app = await DaprHarnessBuilder.ForHarness(harness)
                .ConfigureServices(builder =>
                {
                    var workflowBuilder = builder.Services.AddDaprWorkflowBuilder(
                        configureRuntime: runtime =>
                        {
                            runtime.RegisterWorkflow<ResolvePayloadWorkflow>();
                            runtime.RegisterActivity<ResolvePayloadActivity>();
                            runtime.RegisterWorkflow<AutomaticPayloadWorkflow>();
                            runtime.RegisterWorkflow<EchoPayloadChild>();
                            runtime.RegisterActivity<EchoPayloadActivity>();
                            runtime.RegisterWorkflow<UnconfiguredPayloadWorkflow>();
                            runtime.RegisterWorkflow<UnconfiguredEnvelopeWorkflow>();
                            runtime.RegisterActivity<EchoEnvelopeActivity>();
                        },
                        configureClient: (services, clientBuilder) =>
                        {
                            var grpcEndpoint = services.GetRequiredService<IConfiguration>()["DAPR_GRPC_ENDPOINT"];
                            if (!string.IsNullOrEmpty(grpcEndpoint))
                            {
                                clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                            }
                        });
                    if (enablePayloadSupport)
                    {
                        workflowBuilder.WithBinaryStorePayloads(payloadOptions =>
                        {
                            payloadOptions.StoreName = StoreName;
                            payloadOptions.WritePreference = PayloadWritePreference.PreferReference;
                            payloadOptions.KeyPrefix = KeyPrefix;
                            payloadOptions.OffloadThreshold = 1024;
                            payloadOptions.OffloadSites = PayloadKind.All;
                        });
                        builder.Services.WithPayloadManifest<InMemoryPayloadManifest>();
                    }

                    configureServices?.Invoke(builder.Services);
                })
                .BuildAndStartAsync();

            return (environment, app);
        }
        catch
        {
            try
            {
                await harness.DisposeAsync();
            }
            finally
            {
                await environment.DisposeAsync();
            }

            throw;
        }
    }

    internal sealed record PayloadDocument(string Name, string Content);

    private sealed class InMemoryPayloadManifest : IWorkflowPayloadManifest
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, PayloadReference>> _references =
            new();

        public Task RecordAsync(string instanceId, PayloadReference reference,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _references.GetOrAdd(instanceId, _ => new ConcurrentDictionary<string, PayloadReference>())
                .TryAdd(reference.Key, reference);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<PayloadReference>> ListAsync(string instanceId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyCollection<PayloadReference> references = _references.TryGetValue(instanceId, out var values)
                ? values.Values.ToArray()
                : Array.Empty<PayloadReference>();
            return Task.FromResult(references);
        }

        public Task ClearAsync(string instanceId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _references.TryRemove(instanceId, out _);
            return Task.CompletedTask;
        }
    }

    internal sealed class ResolvePayloadWorkflow : Workflow<PayloadReference<PayloadDocument>, string>
    {
        public override async Task<string> RunAsync(WorkflowContext context, PayloadReference<PayloadDocument> input)
        {
            var payload = await context.ResolvePayloadAsync(input);
            await using (var stream = await context.OpenPayloadAsync(input))
            using (var copy = new MemoryStream())
            {
                await stream.CopyToAsync(copy);
                Assert.Equal(input.Size, copy.Length);
            }

            var result = await context.CallActivityAsync<string>(nameof(ResolvePayloadActivity), input);
            Assert.Equal($"{payload.Name}:{payload.Content.Length}", result);
            return result;
        }
    }

    internal sealed class AutomaticPayloadWorkflow : Workflow<PayloadDocument, PayloadDocument>
    {
        public override async Task<PayloadDocument> RunAsync(WorkflowContext context, PayloadDocument input)
        {
            context.SetCustomStatus(input);
            if (input.Name == "continued")
                return input;
            var activityResult = await context.CallActivityAsync<PayloadDocument>(nameof(EchoPayloadActivity), input);
            Assert.Equal(input with { Name = "activity" }, activityResult);
            var childResult = await context.CallChildWorkflowAsync<PayloadDocument>(nameof(EchoPayloadChild), activityResult);
            Assert.Equal(input with { Name = "child" }, childResult);
            var raised = await context.WaitForExternalEventAsync<PayloadDocument>("payload");
            Assert.Equal(input with { Name = "event" }, raised);
            context.ContinueAsNew(raised with { Name = "continued" });
            return raised;
        }
    }

    internal sealed class UnconfiguredPayloadWorkflow : Workflow<PayloadDocument, PayloadDocument>
    {
        public override Task<PayloadDocument> RunAsync(WorkflowContext context, PayloadDocument input)
        {
            Assert.False(context.HasPayloadResolver());
            return new AutomaticPayloadWorkflow().RunAsync(context, input);
        }
    }

    internal sealed class UnconfiguredEnvelopeWorkflow : Workflow<JsonElement, JsonElement>
    {
        public override async Task<JsonElement> RunAsync(WorkflowContext context, JsonElement input)
        {
            Assert.False(context.HasPayloadResolver());
            context.SetCustomStatus(input);
            return await context.CallActivityAsync<JsonElement>(nameof(EchoEnvelopeActivity), input);
        }
    }

    private sealed class EchoEnvelopeActivity : WorkflowActivity<JsonElement, JsonElement>
    {
        public override Task<JsonElement> RunAsync(WorkflowActivityContext context, JsonElement input)
            => Task.FromResult(input);
    }

    internal sealed class EchoPayloadChild : Workflow<PayloadDocument, PayloadDocument>
    {
        public override Task<PayloadDocument> RunAsync(WorkflowContext context, PayloadDocument input)
            => Task.FromResult(input with { Name = "child" });
    }

    private sealed class EchoPayloadActivity : WorkflowActivity<PayloadDocument, PayloadDocument>
    {
        public override Task<PayloadDocument> RunAsync(WorkflowActivityContext context, PayloadDocument input)
            => Task.FromResult(input with { Name = "activity" });
    }

    private sealed class ResolvePayloadActivity(IWorkflowPayloadResolver resolver)
        : WorkflowActivity<PayloadReference<PayloadDocument>, string>
    {
        public override async Task<string> RunAsync(
            WorkflowActivityContext context,
            PayloadReference<PayloadDocument> input)
        {
            var payload = await resolver.ResolveAsync<PayloadDocument>(input);
            return $"{payload.Name}:{payload.Content.Length}";
        }
    }
}
