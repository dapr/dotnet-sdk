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

using System.Security.Cryptography;
using System.Reflection;
using System.Text;
using Dapr.BinaryStore;
using Dapr.Common.Serialization;
using Dapr.DurableTask.Protobuf;
using Dapr.Workflow.Payloads;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace Dapr.Workflow.Test.Payloads;

public class BinaryStorePayloadTests
{
    private static readonly PayloadSite Site = new(PayloadKind.ActivityInput, "instance", "activity");

    [Fact]
    public void PublicPayloadSurfaceIsLimitedToApplicationContracts()
    {
        Type[] expected =
        [
            typeof(BinaryStorePayloadOptions),
            typeof(IWorkflowPayloadManifest),
            typeof(IWorkflowPayloadResolver),
            typeof(PayloadIntegrityException),
            typeof(PayloadKind),
            typeof(PayloadWritePreference),
            typeof(PayloadNotFoundException),
            typeof(PayloadReference),
            typeof(PayloadReference<>),
            typeof(PayloadStage),
            typeof(WorkflowPayloadCleaner),
            typeof(BinaryStorePayloadServiceCollectionExtensions),
            typeof(WorkflowContextPayloadExtensions)
        ];
        var exported = typeof(DaprWorkflowClient).Assembly.GetExportedTypes()
            .Where(type => type.Namespace == typeof(PayloadReference).Namespace ||
                           type.Name.Contains("Payload", StringComparison.Ordinal) ||
                           type.Name.Contains("WorkflowFeature", StringComparison.Ordinal));

        Assert.Equal(expected.Select(type => type.FullName).OrderBy(name => name, StringComparer.Ordinal),
            exported.Select(type => type.FullName).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Empty(typeof(WorkflowPayloadCleaner).GetConstructors());
        Assert.Null(typeof(BinaryStorePayloadOptions).GetMethod("Validate"));
        Assert.Null(typeof(PayloadReference).GetMethod("IsReferenceType"));
        Assert.Null(typeof(PayloadReference).GetMethod("LooksLikeEnvelope"));

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                                   BindingFlags.DeclaredOnly;
        var publicCallables = expected.SelectMany(type =>
            type.GetMethods(flags).Cast<MethodBase>().Concat(type.GetConstructors(flags)));
        foreach (var callable in publicCallables)
        {
            if (callable is MethodInfo method)
            {
                Assert.DoesNotContain(ReferencedTypes(method.ReturnType),
                    type => type.Namespace?.StartsWith("Dapr.BinaryStore", StringComparison.Ordinal) == true);
            }

            foreach (var parameter in callable.GetParameters())
            {
                Assert.DoesNotContain(ReferencedTypes(parameter.ParameterType),
                    type => type.Namespace?.StartsWith("Dapr.BinaryStore", StringComparison.Ordinal) == true);
            }
        }
    }

    [Fact]
    public void FeatureHooksDoNotExtendThePublicWorkflowContext()
    {
        Assert.Null(typeof(WorkflowContext).GetMethod("GetFeature"));
        Assert.False(typeof(IWorkflowFeatureProvider).IsVisible);
        Assert.False(typeof(IWorkflowFeatureRegistry).IsVisible);
        Assert.False(typeof(IWorkflowFeatureSource).IsVisible);

        var context = new Mock<WorkflowContext>().Object;
        Assert.False(context.HasPayloadResolver());
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = context.ResolvePayloadAsync(new PayloadReference<string>("store", "key", 1, "hash"));
        });
    }

    [Fact]
    public void ImplementationIsInternalToWorkflowAssembly()
    {
        var assembly = typeof(DaprWorkflowClient).Assembly;
        Assert.Same(assembly, typeof(BinaryStorePayloadConverter).Assembly);
        Assert.Same(assembly, typeof(IWorkflowPayloadResolver).Assembly);
        Assert.Same(assembly, typeof(BinaryStorePayloadOptions).Assembly);
        Assert.False(typeof(BinaryStorePayloadConverter).IsPublic);
        Assert.False(typeof(IWorkflowPayloadConverter).IsPublic);
        Assert.False(typeof(BinaryStoreWorkflowPayloadResolver).IsPublic);
        Assert.False(typeof(WorkflowPayloadHandler).IsPublic);
        Assert.False(typeof(NullWorkflowPayloadManifest).IsPublic);
        Assert.False(typeof(BinaryStorePayloadFeatureProvider).IsPublic);
        Assert.False(typeof(BinaryStorePayloadStream).IsPublic);
        Assert.False(typeof(CachingWorkflowPayloadResolver).IsPublic);
        Assert.False(typeof(QueuedWorkflowPayloadResolver).IsVisible);
        Assert.False(typeof(PayloadReferenceParser).IsVisible);
        Assert.False(typeof(PreparedWorkflowPayload).IsVisible);
        Assert.False(typeof(PayloadRehydrationLimitException).IsVisible);
        Assert.False(typeof(PayloadNotResolvableException).IsVisible);
        Assert.False(typeof(PayloadSite).IsVisible);
        Assert.Empty(typeof(WorkflowPayloadCleaner).GetConstructors());
    }

    [Fact]
    public void WorkflowRegistrationDoesNotEnablePayloadStorage()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDaprWorkflow();

        using var provider = services.BuildServiceProvider();
        Assert.Null(provider.GetService<IWorkflowPayloadResolver>());
        Assert.Null(provider.GetService<DaprBinaryStoreClient>());
        Assert.Null(provider.GetService<WorkflowPayloadCleaner>());
    }

    [Theory]
    [InlineData("defaults")]
    [InlineData("configured")]
    [InlineData("builder")]
    public void PayloadRegistrationUsesInternalImplementationAndCustomManifest(string registration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = registration switch
        {
            "defaults" => services.AddDaprWorkflow(),
            "configured" => services.AddDaprWorkflow(_ => { }),
            "builder" => services.AddDaprWorkflowBuilder(null),
            _ => throw new ArgumentOutOfRangeException(nameof(registration))
        };
        var serializer = new JsonDaprSerializer();
        var configuredBuilder = builder
            .WithBinaryStorePayloads(options => options.StoreName = "store")
            .WithSerializer(serializer);
        Assert.Same(services, configuredBuilder.Services);
        services.WithPayloadManifest<TestManifest>();
        services.AddSingleton(new Mock<DaprBinaryStoreClient>(MockBehavior.Strict).Object);

        using var provider = services.BuildServiceProvider();
        Assert.Same(serializer, provider.GetRequiredService<IDaprSerializer>());
        Assert.Equal("store", provider.GetRequiredService<IOptions<BinaryStorePayloadOptions>>().Value.StoreName);
        var converter = provider.GetRequiredService<IWorkflowPayloadConverter>();
        Assert.IsType<BinaryStorePayloadConverter>(converter);
        Assert.IsType<BinaryStoreWorkflowPayloadResolver>(provider.GetRequiredService<IWorkflowPayloadResolver>());
        Assert.IsType<TestManifest>(provider.GetRequiredService<IWorkflowPayloadManifest>());
        Assert.NotNull(provider.GetRequiredService<WorkflowPayloadCleaner>());
        Assert.Contains(provider.GetServices<IWorkflowFeatureProvider>(),
            feature => feature is BinaryStorePayloadFeatureProvider);
    }

    [Fact]
    public void PayloadRegistrationRequiresWorkflowBuilder()
    {
        var method = typeof(BinaryStorePayloadServiceCollectionExtensions)
            .GetMethod(nameof(BinaryStorePayloadServiceCollectionExtensions.WithBinaryStorePayloads))!;

        Assert.Equal(typeof(WorkflowServiceCollectionExtensions.DaprWorkflowBuilder),
            method.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(WorkflowServiceCollectionExtensions.DaprWorkflowBuilder), method.ReturnType);
    }

    [Fact]
    public void PayloadRegistrationRejectsUninitializedBuilder()
    {
        var builder = default(WorkflowServiceCollectionExtensions.DaprWorkflowBuilder);

        Assert.Throws<ArgumentNullException>(() =>
            builder.WithBinaryStorePayloads(options => options.StoreName = "store"));
    }

    [Fact]
    public void PayloadRegistrationRejectsNullConfiguration()
    {
        var services = new ServiceCollection();
        var builder = services.AddDaprWorkflow();
        var count = services.Count;

        Assert.Throws<ArgumentNullException>(() => builder.WithBinaryStorePayloads(null!));
        Assert.Equal(count, services.Count);
    }

    [Fact]
    public async Task InlinePayloadPreservesConfiguredSerializer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var serializer = new JsonDaprSerializer(new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = null
        });
        var converter = CreateConverter(store.Object);
        var value = new PayloadDocument("small");

        var wire = await converter.EncodeAsync(serializer.Serialize(value), Site, cancellationToken);

        Assert.Equal(serializer.Serialize(value), wire);
        Assert.Equal(value, serializer.Deserialize<PayloadDocument>(
            await converter.DecodeAsync(wire, Site, cancellationToken)));
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OffloadingPreservesVersionOneEnvelopeAndStoredBytes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var serializer = new JsonDaprSerializer();
        var value = new PayloadDocument(new string('x', 128 * 1024));
        var bytes = Encoding.UTF8.GetBytes(serializer.Serialize(value));
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var key = $"workflow-payloads/instance/{hash}";
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        store.Setup(s => s.SetBinaryFileAsync("store", key, It.IsAny<Stream>(), false,
                DaprBinaryStoreClient.DefaultChunkSize, It.IsAny<CancellationToken>()))
            .Returns(async (string _, string _, Stream content, bool _, int _, CancellationToken cancellationToken) =>
            {
                using var copy = new MemoryStream();
                await content.CopyToAsync(copy, cancellationToken);
                Assert.Equal(bytes, copy.ToArray());
            });
        store.Setup(s => s.GetBinaryFileChunksAsync("store", key, It.IsAny<CancellationToken>()))
            .Returns(() => ReadChunksAsync(bytes));
        var manifest = new TestManifest();
        var converter = CreateConverter(store.Object, manifest);

        var wire = await converter.EncodeAsync(serializer.Serialize(value), Site, cancellationToken);

        Assert.Equal(
            $"{{\"$daprRef\":{{\"v\":1,\"store\":\"store\",\"key\":\"{key}\",\"size\":{bytes.Length},\"sha256\":\"{hash}\"}}}}",
            wire);
        Assert.Equal(value, serializer.Deserialize<PayloadDocument>(
            await converter.DecodeAsync(wire, Site, cancellationToken)));
        var recorded = Assert.Single(manifest.References);
        Assert.Equal("instance", recorded.InstanceId);
        Assert.Equal(key, recorded.Reference.Key);
        store.VerifyAll();
    }

    [Theory]
    [InlineData(PayloadWritePreference.Preserve)]
    [InlineData(PayloadWritePreference.PreferReference)]
    public async Task ExplicitReferenceIsPassedThroughWithoutStorageIo(PayloadWritePreference preference)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var converter = CreateConverter(store.Object, preference: preference);
        var handler = new WorkflowPayloadHandler(new JsonDaprSerializer(), converter);
        var reference = new PayloadReference<string>("legacy-store", "legacy-key", 10, "hash");

        var wire = await converter.EncodeAsync(handler.Snapshot(reference), Site, cancellationToken);
        var typed = await handler.DecodeAsync<PayloadReference<string>>(wire, Site, cancellationToken);
        var untyped = await handler.DecodeAsync<PayloadReference>(wire, Site, cancellationToken);

        Assert.Equal(reference.ToEnvelope(), wire);
        Assert.Equal(reference.ToEnvelope(), typed!.ToEnvelope());
        Assert.Equal(reference.ToEnvelope(), untyped!.ToEnvelope());
        store.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(PayloadWritePreference.Preserve)]
    [InlineData(PayloadWritePreference.PreferReference)]
    [InlineData(PayloadWritePreference.PreferInline)]
    public async Task ResolverCanStoreAndStreamWithoutAutomaticBoundaryInterception(PayloadWritePreference preference)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var bytes = Encoding.UTF8.GetBytes("\"value\"");
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var key = $"workflow-payloads/instance/{hash}";
        store.Setup(s => s.SetBinaryFileAsync("store", key, It.IsAny<Stream>(), false,
                DaprBinaryStoreClient.DefaultChunkSize, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.Setup(s => s.GetBinaryFileChunksAsync("store", key, It.IsAny<CancellationToken>()))
            .Returns(() => ReadChunksAsync(bytes));
        IWorkflowPayloadResolver resolver = new BinaryStoreWorkflowPayloadResolver(
            CreateConverter(store.Object, preference: preference), new JsonDaprSerializer());

        var reference = await resolver.StoreAsync("value", "instance", cancellationToken);
        Assert.Equal("value", await resolver.ResolveAsync<string>(reference, cancellationToken));
        await using var stream = await resolver.OpenAsync(reference, cancellationToken);
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy, cancellationToken);
        Assert.Equal(bytes, copy.ToArray());
    }

    [Fact]
    public async Task CleanupUsesExplicitManifestAndClearsItAfterDeletion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var reference = new PayloadReference("store", "key", 10, "hash");
        var manifest = new TestManifest();
        await manifest.RecordAsync("instance", reference, cancellationToken);
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        store.Setup(s => s.DeleteBinaryFileAsync("store", "key", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var cleaner = new WorkflowPayloadCleaner(store.Object, manifest);

        Assert.Equal(1, await cleaner.PurgeAsync("instance", cancellationToken));
        Assert.Empty(manifest.References);
        store.VerifyAll();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("custom serialized payload")]
    [InlineData("{custom non-JSON serialization}")]
    [InlineData("{\"user\":{\"$daprRef\":{}}}")]
    [InlineData("{\"$daprRef\":\"user-property\",\"other\":true}")]
    public async Task CodecPreservesOrdinarySerializedValues(string? serialized)
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var codec = CreateConverter(store.Object);
        var token = TestContext.Current.CancellationToken;
        Assert.Equal(serialized, await codec.DecodeAsync(serialized, Site, token));
        if (serialized is not null)
            Assert.Equal(serialized, await codec.EncodeAsync(serialized, Site, token));
        store.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("{\"$daprRef\":{\"v\":2,\"store\":\"s\",\"key\":\"k\",\"size\":1,\"sha256\":\"h\"}}")]
    [InlineData("{\"$daprRef\":{\"v\":0,\"store\":\"s\",\"key\":\"k\",\"size\":1,\"sha256\":\"h\"}}")]
    [InlineData("{\"$daprRef\":{\"store\":\"s\",\"key\":\"k\",\"size\":1,\"sha256\":\"h\"}}")]
    [InlineData("{\"$daprRef\":{\"v\":1,\"store\":null,\"key\":\"k\",\"size\":1,\"sha256\":\"h\"}}")]
    [InlineData("{\"$daprRef\":{\"v\":1,\"store\":\"s\",\"key\":\"k\",\"size\":-1,\"sha256\":\"h\"}}")]
    [InlineData("{\"$daprRef\":null}")]
    [InlineData("{\"$daprRef\":")]
    public async Task CodecRejectsInvalidOrUnsupportedEnvelopes(string wire)
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var codec = CreateConverter(store.Object);
        var token = TestContext.Current.CancellationToken;
        Assert.NotNull(await Record.ExceptionAsync(async () => await codec.DecodeAsync(wire, Site, token)));
        Assert.NotNull(await Record.ExceptionAsync(async () => await codec.EncodeAsync(wire, Site, token)));
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EnvelopeDetectionDoesNotDependOnLeadingWhitespaceOrSerializer()
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var codec = CreateConverter(store.Object);
        var reference = new PayloadReference("store", "key", 12, "hash");
        var wire = " \n{" + new string(' ', 128) + reference.ToEnvelope()[1..];
        Assert.Equal(reference, PayloadReferenceParser.Parse(wire));
        Assert.Equal(wire, await codec.EncodeAsync(wire, Site, TestContext.Current.CancellationToken));
        store.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(4, PayloadKind.ActivityInput, true)]
    [InlineData(5, PayloadKind.ActivityInput, false)]
    [InlineData(4, PayloadKind.WorkflowInput, false)]
    public async Task OffloadPolicyUsesUtf8ByteCountAndSelectedSite(int threshold, PayloadKind site, bool offloaded)
    {
        const string serialized = "\u00e9\u00e9";
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        store.Setup(s => s.SetBinaryFileAsync("store", It.IsAny<string>(), It.IsAny<Stream>(), false,
                DaprBinaryStoreClient.DefaultChunkSize, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var codec = new BinaryStorePayloadConverter(store.Object,
            Options.Create(new BinaryStorePayloadOptions
            {
                StoreName = "store", OffloadThreshold = threshold, OffloadSites = PayloadKind.ActivityInput,
                WritePreference = PayloadWritePreference.PreferReference
            }), NullWorkflowPayloadManifest.Instance);
        var wire = await codec.EncodeAsync(serialized, Site with { Kind = site },
            TestContext.Current.CancellationToken);
        Assert.Equal(offloaded, PayloadReference.TryParse(wire, out _));
        store.Verify(s => s.SetBinaryFileAsync("store", It.IsAny<string>(), It.IsAny<Stream>(), false,
                DaprBinaryStoreClient.DefaultChunkSize, It.IsAny<CancellationToken>()),
            offloaded ? Times.Once() : Times.Never());
    }

    [Theory]
    [InlineData(PayloadWritePreference.Preserve, null)]
    [InlineData(PayloadWritePreference.Preserve, 1)]
    [InlineData(PayloadWritePreference.Preserve, 64 * 1024)]
    [InlineData(PayloadWritePreference.PreferInline, null)]
    public async Task NonOffloadingPreferencesWriteInlineAtEveryBoundary(PayloadWritePreference preference, int? threshold)
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var manifest = new TestManifest();
        var options = new BinaryStorePayloadOptions
        {
            StoreName = "store", OffloadThreshold = threshold, OffloadSites = PayloadKind.All
        };
        Assert.Equal(PayloadWritePreference.Preserve, options.WritePreference);
        options.WritePreference = preference;
        var codec = new BinaryStorePayloadConverter(store.Object, Options.Create(options), manifest);
        var serializer = new JsonDaprSerializer();
        var value = new PayloadDocument(new string('x', 128 * 1024));
        var serialized = serializer.Serialize(value);
        var token = TestContext.Current.CancellationToken;

        foreach (var kind in Enum.GetValues<PayloadKind>().Except([PayloadKind.None, PayloadKind.All]))
        {
            var site = Site with { Kind = kind };
            var wire = await codec.EncodeAsync(serialized, site, token);
            Assert.Equal(serialized, wire);
            Assert.False(PayloadReference.TryParse(wire, out _));
            Assert.Equal(value, serializer.Deserialize<PayloadDocument>(await codec.DecodeAsync(wire, site, token)));
        }

        Assert.Empty(manifest.References);
        store.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task OversizedMetadataPreservesReferenceButRejectsMaterializedReads(long declaredSize)
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        store.Setup(s => s.GetBinaryFileChunksAsync("store", "key", It.IsAny<CancellationToken>()))
            .Returns(() => ReadChunksAsync(new byte[100]));
        var codec = new BinaryStorePayloadConverter(store.Object,
            Options.Create(new BinaryStorePayloadOptions { StoreName = "store", MaxInMemoryRehydrationSize = 10 }),
            NullWorkflowPayloadManifest.Instance);
        var serializer = new JsonDaprSerializer();
        var handler = new WorkflowPayloadHandler(serializer, codec);
        var reference = new PayloadReference("store", "key", declaredSize, "hash");
        var prepared = await handler.PrepareAsync(reference.ToEnvelope(), Site, TestContext.Current.CancellationToken);

        Assert.Equal(reference.ToEnvelope(), prepared.ReadAs<PayloadReference<string>>(serializer)!.ToEnvelope());
        Assert.Throws<PayloadRehydrationLimitException>(() => prepared.ReadAs<string>(serializer));
        store.Verify(s => s.GetBinaryFileChunksAsync("store", "key", It.IsAny<CancellationToken>()),
            declaredSize > 10 ? Times.Never() : Times.Once());
    }

    [Fact]
    public async Task ReferenceBoundaryAlsoAcceptsLegacyObjectSerialization()
    {
        var serializer = new JsonDaprSerializer();
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var handler = new WorkflowPayloadHandler(serializer, CreateConverter(store.Object));
        var reference = new PayloadReference<string>("store", "key", 12, "hash");
        var restored = await handler.DecodeAsync<PayloadReference<string>>(serializer.Serialize(reference),
            Site, TestContext.Current.CancellationToken);
        Assert.Equal(reference.ToEnvelope(), restored!.ToEnvelope());
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResponseEncodingCoversAllActionKindsWithoutChangingCarryoverHistory()
    {
        var codec = new DelayedPayloadConverter();
        var handler = new WorkflowPayloadHandler(new JsonDaprSerializer(), codec);
        var carried = new Dapr.DurableTask.Protobuf.HistoryEvent
        {
            EventRaised = new() { Name = "event", Input = "wire:retained" }
        };
        var response = new Dapr.DurableTask.Protobuf.WorkflowResponse
        {
            CustomStatus = "status",
            Actions =
            {
                new WorkflowAction { ScheduleTask = new() { Name = "activity", Input = "activity-input" } },
                new WorkflowAction
                {
                    CreateChildWorkflow = new()
                    {
                        InstanceId = "child", Name = "child", Input = "child-input"
                    }
                },
                new WorkflowAction
                {
                    CreateDetachedWorkflow = new()
                    {
                        InstanceId = "detached", Name = "detached", Input = "detached-input"
                    }
                },
                new WorkflowAction
                {
                    SendEvent = new()
                    {
                        Instance = new() { InstanceId = "target" }, Name = "event", Data = "event-data"
                    }
                },
                new WorkflowAction
                {
                    CompleteWorkflow = new()
                    {
                        WorkflowStatus = Dapr.DurableTask.Protobuf.OrchestrationStatus.ContinuedAsNew,
                        Result = "continued-input",
                        CarryoverEvents = { carried }
                    }
                },
                new WorkflowAction
                {
                    CompleteWorkflow = new()
                    {
                        WorkflowStatus = Dapr.DurableTask.Protobuf.OrchestrationStatus.Completed,
                        Result = "output"
                    }
                }
            }
        };

        await handler.EncodeResponseAsync(response, "parent", "workflow", false, TestContext.Current.CancellationToken);

        Assert.Equal([
            PayloadKind.ActivityInput, PayloadKind.ChildWorkflowInput, PayloadKind.WorkflowInput,
            PayloadKind.ExternalEvent, PayloadKind.ContinueAsNewInput, PayloadKind.WorkflowOutput,
            PayloadKind.CustomStatus
        ], codec.Writes.Select(write => write.Site.Kind));
        Assert.Equal(["parent", "child", "detached", "target", "parent", "parent", "parent"],
            codec.Writes.Select(write => write.Site.InstanceId));
        Assert.Equal("wire:continued-input", response.Actions[4].CompleteWorkflow.Result);
        Assert.Equal("wire:retained", carried.EventRaised.Input);
    }

    [Fact]
    public async Task DisablingNewWritesStillReadsExistingVersionOneReferences()
    {
        var bytes = Encoding.UTF8.GetBytes("\"retained\"");
        var reference = new PayloadReference("legacy-store", "legacy-key", bytes.Length,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        store.Setup(s => s.GetBinaryFileChunksAsync("legacy-store", "legacy-key", It.IsAny<CancellationToken>()))
            .Returns(() => ReadChunksAsync(bytes));
        var codec = new BinaryStorePayloadConverter(store.Object,
            Options.Create(new BinaryStorePayloadOptions
            {
                StoreName = "new-store", OffloadThreshold = null, OffloadSites = PayloadKind.None
            }), NullWorkflowPayloadManifest.Instance);
        var token = TestContext.Current.CancellationToken;

        Assert.Equal("\"retained\"", await codec.DecodeAsync(reference.ToEnvelope(), Site, token));
        Assert.Equal("\"new\"", await codec.EncodeAsync("\"new\"", Site, token));
        store.Verify(s => s.SetBinaryFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), false,
            DaprBinaryStoreClient.DefaultChunkSize, It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task ResolutionCacheDoesNotIgnoreChangedReferenceMetadata()
    {
        var original = new PayloadReference("store", "key", 10, "original-hash");
        var changed = original with { ContentHash = "different-hash" };
        var inner = new Mock<IWorkflowPayloadResolver>(MockBehavior.Strict);
        inner.Setup(r => r.ResolveAsync<string>(original, It.IsAny<CancellationToken>())).ReturnsAsync("value");
        inner.Setup(r => r.ResolveAsync<string>(changed, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PayloadIntegrityException(changed, original.ContentHash));
        var resolver = new CachingWorkflowPayloadResolver(inner.Object, 10);
        var token = TestContext.Current.CancellationToken;

        Assert.Equal("value", await resolver.ResolveAsync<string>(original, token));
        Assert.Equal("value", await resolver.ResolveAsync<string>(original, token));
        await Assert.ThrowsAsync<PayloadIntegrityException>(() => resolver.ResolveAsync<string>(changed, token));
        inner.Verify(r => r.ResolveAsync<string>(original, It.IsAny<CancellationToken>()), Times.Once());
    }

    private static BinaryStorePayloadConverter CreateConverter(DaprBinaryStoreClient store,
        IWorkflowPayloadManifest? manifest = null, PayloadWritePreference preference = PayloadWritePreference.PreferReference) =>
        new(store, Options.Create(new BinaryStorePayloadOptions
        {
            StoreName = "store", WritePreference = preference
        }),
            manifest ?? NullWorkflowPayloadManifest.Instance);

    private static IEnumerable<Type> ReferencedTypes(Type type)
    {
        yield return type;
        if (type.HasElementType)
        {
            foreach (var element in ReferencedTypes(type.GetElementType()!))
                yield return element;
        }

        foreach (var argument in type.GetGenericArguments())
        foreach (var nested in ReferencedTypes(argument))
            yield return nested;
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadChunksAsync(byte[] bytes)
    {
        await Task.Yield();
        yield return bytes.AsMemory(0, bytes.Length / 2);
        yield return bytes.AsMemory(bytes.Length / 2);
    }

    internal sealed class DelayedPayloadConverter : IWorkflowPayloadConverter
    {
        public List<(string Serialized, PayloadSite Site)> Writes { get; } = [];
        public List<PayloadSite> Reads { get; } = [];

        public async ValueTask<string> EncodeAsync(string serialized, PayloadSite site,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            Writes.Add((serialized, site));
            return "wire:" + serialized;
        }

        public async ValueTask<string?> DecodeAsync(string? wire, PayloadSite site,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            Reads.Add(site);
            return wire?.StartsWith("wire:", StringComparison.Ordinal) == true ? wire[5..] : wire;
        }
    }

    private sealed record PayloadDocument(string Content);

    public sealed class TestManifest : IWorkflowPayloadManifest
    {
        public List<(string InstanceId, PayloadReference Reference)> References { get; } = [];

        public Task RecordAsync(string instanceId, PayloadReference reference,
            CancellationToken cancellationToken = default)
        {
            References.Add((instanceId, reference));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<PayloadReference>> ListAsync(string instanceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<PayloadReference>>(
                References.Where(r => r.InstanceId == instanceId).Select(r => r.Reference).ToArray());

        public Task ClearAsync(string instanceId, CancellationToken cancellationToken = default)
        {
            References.RemoveAll(r => r.InstanceId == instanceId);
            return Task.CompletedTask;
        }
    }
}
