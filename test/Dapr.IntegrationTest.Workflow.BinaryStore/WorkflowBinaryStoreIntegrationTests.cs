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
using Dapr.Workflow.BinaryStore;
using Dapr.Workflow.BinaryStore.Extensions;
using Dapr.Workflow.Payloads;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Workflow.BinaryStore;

public sealed class WorkflowBinaryStoreIntegrationTests
{
    private const string StoreName = Constants.DaprComponentNames.BinaryStoreComponentName;
    private const string KeyPrefix = "workflow-integration";

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
        var converter = scope.ServiceProvider.GetRequiredService<IWorkflowPayloadConverter>();
        var site = new PayloadSite(PayloadKind.ActivityInput, instanceId, nameof(ResolvePayloadActivity));

        var wire = await converter.ToWireAsync(payload, site, cancellationToken);
        Assert.True(PayloadReference.TryParse(wire, out var reference));
        Assert.NotNull(reference);
        Assert.Equal(StoreName, reference.StoreName);
        Assert.True(reference.Size > payload.Content.Length);
        Assert.StartsWith($"{KeyPrefix}/{instanceId}/", reference.Key, StringComparison.Ordinal);

        var repeatedWire = await converter.ToWireAsync(payload, site, cancellationToken);
        Assert.Equal(wire, repeatedWire);
        Assert.False(PayloadReference.TryParse(
            await converter.ToWireAsync(new PayloadDocument("small", "small"), site, cancellationToken),
            out _));
        Assert.False(PayloadReference.TryParse(
            await converter.ToWireAsync(payload, site with { Kind = PayloadKind.WorkflowOutput }, cancellationToken),
            out _));

        var binaryStore = scope.ServiceProvider.GetRequiredService<DaprBinaryStoreClient>();
        var serialized =
            Encoding.UTF8.GetBytes(scope.ServiceProvider.GetRequiredService<IDaprSerializer>().Serialize(payload));
        Assert.Equal(serialized.LongLength, reference.Size);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(serialized)).ToLowerInvariant(), reference.ContentHash);
        Assert.Equal(serialized,
            await binaryStore.GetBinaryFileBytesAsync(reference.StoreName, reference.Key, cancellationToken));

        var hydrated = await converter.FromWireAsync<PayloadDocument>(wire, site, cancellationToken);
        Assert.Equal(payload, hydrated);

        var resolver = scope.ServiceProvider.GetRequiredService<IWorkflowPayloadResolver>();
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

    private static async Task<(DaprTestEnvironment Environment, DaprTestApplication App)> StartAsync()
    {
        var componentsDirectory = TestDirectoryManager.CreateTestDirectory("workflow-binary-store-components");
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
                    builder.Services.AddDaprWorkflowBuilder(
                        configureRuntime: runtime =>
                        {
                            runtime.RegisterWorkflow<ResolvePayloadWorkflow>();
                            runtime.RegisterActivity<ResolvePayloadActivity>();
                        },
                        configureClient: (services, clientBuilder) =>
                        {
                            var grpcEndpoint = services.GetRequiredService<IConfiguration>()["DAPR_GRPC_ENDPOINT"];
                            if (!string.IsNullOrEmpty(grpcEndpoint))
                            {
                                clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                            }
                        });
                    builder.Services.WithBinaryStorePayloads(payloadOptions =>
                    {
                        payloadOptions.StoreName = StoreName;
                        payloadOptions.KeyPrefix = KeyPrefix;
                        payloadOptions.OffloadThreshold = 1024;
                        payloadOptions.OffloadSites = PayloadKind.ActivityInput;
                    });
                    builder.Services.WithPayloadManifest<InMemoryPayloadManifest>();
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

    private sealed record PayloadDocument(string Name, string Content);

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

    private sealed class ResolvePayloadWorkflow : Workflow<PayloadReference<PayloadDocument>, string>
    {
        public override Task<string> RunAsync(WorkflowContext context, PayloadReference<PayloadDocument> input)
            => context.CallActivityAsync<string>(nameof(ResolvePayloadActivity), input);
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
