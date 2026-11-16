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
using System.Text;
using Dapr.BinaryStore;
using Dapr.Common.Serialization;
using Dapr.Workflow.Payloads;
using Microsoft.Extensions.Options;
using Moq;

namespace Dapr.Workflow.Test.Payloads;

public class PayloadPrefixFactoryTests
{
    [Fact]
    public void PublicRoutingSurfaceIsLimitedToFactoryAndMetadata()
    {
        Assert.Null(typeof(BinaryStorePayloadOptions).GetProperty("InstancePrefixFactory"));
        Assert.Null(typeof(WorkflowActivityContext).GetProperty("WorkflowName"));
        Assert.Null(typeof(WorkflowActivityContext).GetMethod("GetFeature"));
        Assert.Equal(typeof(Func<WorkflowPayloadPrefixContext, string>),
            typeof(BinaryStorePayloadOptions).GetProperty(nameof(BinaryStorePayloadOptions.PrefixFactory))!.PropertyType);
        var properties = typeof(WorkflowPayloadPrefixContext).GetProperties();
        Assert.Equal(new[] { "ActivityName", "InstanceId", "WorkflowName" },
            properties.Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.All(properties, property => Assert.False(property.CanWrite));
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("", null, null)]
    [InlineData(" ", null, null)]
    [InlineData("instance", "", null)]
    [InlineData("instance", null, " ")]
    public void ContextRejectsBlankIdentifiers(string? id, string? workflow, string? activity)
        => Assert.ThrowsAny<ArgumentException>(() => new WorkflowPayloadPrefixContext(id!, workflow, activity));

    [Theory]
    [InlineData(PayloadKind.WorkflowInput, "workflow", "workflow", null)]
    [InlineData(PayloadKind.WorkflowOutput, "workflow", "workflow", null)]
    [InlineData(PayloadKind.ChildWorkflowInput, "child", "child", null)]
    [InlineData(PayloadKind.ChildWorkflowOutput, "child", "child", null)]
    [InlineData(PayloadKind.ContinueAsNewInput, "workflow", "workflow", null)]
    [InlineData(PayloadKind.ActivityInput, "activity", null, "activity")]
    [InlineData(PayloadKind.ActivityOutput, "activity", null, "activity")]
    [InlineData(PayloadKind.ExternalEvent, "event-not-workflow", null, null)]
    [InlineData(PayloadKind.CustomStatus, null, null, null)]
    [InlineData(PayloadKind.WorkflowInput, null, null, null)]
    public async Task AutomaticWritesDoNotMisidentifyBoundaryNames(PayloadKind kind, string? name,
        string? workflow, string? activity)
    {
        var token = TestContext.Current.CancellationToken;
        var calls = 0;
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        store.Setup(s => s.SetBinaryFileAsync("store", It.IsAny<string>(), It.IsAny<Stream>(), false,
                DaprBinaryStoreClient.DefaultChunkSize, token)).Returns(Task.CompletedTask);
        var codec = new BinaryStorePayloadConverter(store.Object, Options.Create(new BinaryStorePayloadOptions
        {
            StoreName = "store", WritePreference = PayloadWritePreference.PreferReference, OffloadThreshold = null,
            PrefixFactory = context =>
            {
                calls++;
                Assert.Equal(new WorkflowPayloadPrefixContext("instance", workflow, activity), context);
                return "prefix";
            }
        }), NullWorkflowPayloadManifest.Instance);
        await codec.EncodeAsync("\"hello\"", new PayloadSite(kind, "instance", name), token);
        Assert.Equal(1, calls);
        store.VerifyAll();
    }

    [Fact]
    public async Task InstanceIdOverloadsLeaveNamesUnknown()
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var expected = new InvalidOperationException("Workflow name is required.");
        var codec = new BinaryStorePayloadConverter(store.Object, Options.Create(new BinaryStorePayloadOptions
        {
            StoreName = "store",
            PrefixFactory = context =>
            {
                Assert.Equal("instance", context.InstanceId);
                Assert.Null(context.WorkflowName);
                Assert.Null(context.ActivityName);
                throw expected;
            }
        }), NullWorkflowPayloadManifest.Instance);
        IWorkflowPayloadResolver resolver = new BinaryStoreWorkflowPayloadResolver(codec, new JsonDaprSerializer());
        using var source = new MemoryStream(new byte[] { 1 });
        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resolver.UploadAsync(source, "instance", TestContext.Current.CancellationToken)));
        Assert.Equal(0, source.Position);
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LegacyResolversReceiveInstanceIdThroughCompatibilityOverloads()
    {
        var token = TestContext.Current.CancellationToken;
        var context = new WorkflowPayloadPrefixContext("instance", "workflow", "activity");
        var reference = new PayloadReference("store", "key", 0, "hash");
        using var source = new MemoryStream();
        var chunks = new Mock<IAsyncEnumerable<ReadOnlyMemory<byte>>>(MockBehavior.Strict).Object;
        Func<Stream, CancellationToken, Task> write = (_, _) => Task.CompletedTask;
        var legacy = new Mock<IWorkflowPayloadResolver> { CallBase = true };
        legacy.Setup(r => r.StoreAsync(1, "instance", token)).ReturnsAsync(reference.AsTyped<int>());
        legacy.Setup(r => r.StoreStreamAsync(source, "instance", token)).ReturnsAsync(reference);
        legacy.Setup(r => r.UploadAsync(source, "instance", token)).ReturnsAsync(reference);
        legacy.Setup(r => r.UploadAsync(chunks, "instance", token)).ReturnsAsync(reference);
        legacy.Setup(r => r.UploadAsync(write, "instance", token)).ReturnsAsync(reference);
        legacy.Setup(r => r.WriteAsync(write, "instance", token)).ReturnsAsync(reference);
        var resolver = legacy.Object;

        Assert.Equal(reference.AsTyped<int>(), await resolver.StoreAsync(1, context, token));
        Assert.Same(reference, await resolver.StoreStreamAsync(source, context, token));
        Assert.Same(reference, await resolver.UploadAsync(source, context, token));
        Assert.Same(reference, await resolver.UploadAsync(chunks, context, token));
        Assert.Same(reference, await resolver.UploadAsync(write, context, token));
        Assert.Same(reference, await resolver.WriteAsync(write, context, token));
        legacy.VerifyAll();
    }

    [Theory]
    [InlineData("automatic", false)]
    [InlineData("automatic", true)]
    [InlineData("automatic-stream", false)]
    [InlineData("automatic-stream", true)]
    [InlineData("value", false)]
    [InlineData("value", true)]
    [InlineData("staged", false)]
    [InlineData("staged", true)]
    [InlineData("stream", false)]
    [InlineData("stream", true)]
    [InlineData("chunks", false)]
    [InlineData("chunks", true)]
    [InlineData("producer", false)]
    [InlineData("producer", true)]
    [InlineData("writer", true)]
    public async Task EveryWriteUsesFactoryAndPreservesInstanceOwnership(string mode, bool richMetadata)
    {
        var token = TestContext.Current.CancellationToken;
        var calls = new List<string>();
        var options = new BinaryStorePayloadOptions
        {
            StoreName = "store", KeyPrefix = "root",
            WritePreference = PayloadWritePreference.PreferReference, OffloadThreshold = null,
            PrefixFactory = context =>
            {
                calls.Add(context.InstanceId);
                return $"tenants/{context.InstanceId.Split('-')[0]}/instances";
            }
        };
        if (richMetadata)
        {
            options.PrefixFactory = context =>
            {
                calls.Add(context.InstanceId);
                Assert.Equal("workflow", context.WorkflowName);
                Assert.Equal(mode == "automatic-stream" ? null : "activity", context.ActivityName);
                return $"tenants/{context.InstanceId.Split('-')[0]}/workflows/{context.WorkflowName}" +
                       (context.ActivityName is { } activity ? $"/activities/{activity}" : "") + "/instances";
            };
        }
        var bytes = Encoding.UTF8.GetBytes("\"hello\"");
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var keys = new List<string>();
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        store.Setup(s => s.SetBinaryFileAsync("store", It.IsAny<string>(), It.IsAny<Stream>(), false,
                DaprBinaryStoreClient.DefaultChunkSize, It.IsAny<CancellationToken>()))
            .Returns(async (string _, string key, Stream content, bool _, int _, CancellationToken ct) =>
            {
                using var uploaded = new MemoryStream();
                await content.CopyToAsync(uploaded, ct);
                Assert.Equal(bytes, uploaded.ToArray());
                keys.Add(key);
            });
        var manifest = new Mock<IWorkflowPayloadManifest>(MockBehavior.Strict);
        var owners = new List<string>();
        manifest.Setup(m => m.RecordAsync(It.IsAny<string>(), It.IsAny<PayloadReference>(), token))
            .Returns((string id, PayloadReference reference, CancellationToken _) =>
            {
                owners.Add(id);
                Assert.Contains(reference.Key, keys);
                return Task.CompletedTask;
            });
        var codec = new BinaryStorePayloadConverter(store.Object, Options.Create(options), manifest.Object);
        IWorkflowPayloadResolver resolver = new BinaryStoreWorkflowPayloadResolver(codec, new JsonDaprSerializer());
        if (richMetadata)
            resolver = new CachingWorkflowPayloadResolver(resolver, 10);
        foreach (var id in new[] { "acme-1", "acme-2", "bravo-1" })
        {
            var context = new WorkflowPayloadPrefixContext(id, "workflow", "activity");
            using var source = new MemoryStream(bytes);
            async IAsyncEnumerable<ReadOnlyMemory<byte>> Chunks()
            {
                await Task.Yield();
                yield return bytes;
            }
            var reference = mode switch
            {
                "automatic" => PayloadReferenceParser.Parse(await codec.EncodeAsync("\"hello\"",
                    new PayloadSite(PayloadKind.ActivityOutput, id, "activity", WorkflowName: "workflow"), token))!,
                "automatic-stream" => PayloadReferenceParser.Parse(await codec.EncodeStreamAsync(source,
                    new PayloadSite(PayloadKind.WorkflowInput, id, "workflow"), token))!,
                "value" => richMetadata ? await resolver.StoreAsync("hello", context, token) : await resolver.StoreAsync("hello", id, token),
                "staged" => richMetadata ? await resolver.StoreStreamAsync(source, context, token) : await resolver.StoreStreamAsync(source, id, token),
                "stream" => richMetadata ? await resolver.UploadAsync(source, context, token) : await resolver.UploadAsync(source, id, token),
                "chunks" => richMetadata ? await resolver.UploadAsync(Chunks(), context, token) : await resolver.UploadAsync(Chunks(), id, token),
                "writer" => await resolver.WriteAsync((destination, ct) => destination.WriteAsync(bytes, ct).AsTask(), context, token),
                _ => richMetadata
                    ? await resolver.UploadAsync((destination, ct) => destination.WriteAsync(bytes, ct).AsTask(), context, token)
                    : await resolver.UploadAsync((destination, ct) => destination.WriteAsync(bytes, ct).AsTask(), id, token)
            };
            var names = richMetadata
                ? "/workflows/workflow" + (mode == "automatic-stream" ? "" : "/activities/activity")
                : "";
            var prefix = $"root/tenants/{id.Split('-')[0]}{names}/instances/{id}/";
            Assert.StartsWith(prefix, reference.Key);
            Assert.Equal(hash, reference.ContentHash);
            Assert.Equal(bytes.Length, reference.Size);
            if (mode is "stream" or "chunks" or "producer" or "writer")
                Assert.StartsWith(prefix + "uploads/", reference.Key);
            else
                Assert.Equal(prefix + hash, reference.Key);
        }
        Assert.Equal(new[] { "acme-1", "acme-2", "bravo-1" }, calls);
        Assert.Equal(calls, owners);
        store.VerifyAll();
        manifest.VerifyAll();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("/absolute")]
    [InlineData("C:/absolute")]
    [InlineData("trailing/")]
    [InlineData("tenants//acme")]
    [InlineData("tenants/../acme")]
    [InlineData("tenants/./acme")]
    [InlineData("tenants\\acme")]
    [InlineData("tenants/%2e%2e")]
    [InlineData("tenants/\nacme")]
    [InlineData("tenants/acme?")]
    public async Task InvalidFactoryResultFailsBeforeUploadOrSourceConsumption(string? prefix)
    {
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var codec = new BinaryStorePayloadConverter(store.Object, Options.Create(new BinaryStorePayloadOptions
        {
            StoreName = "store", PrefixFactory = _ => prefix!
        }), NullWorkflowPayloadManifest.Instance);
        IWorkflowPayloadResolver resolver = new BinaryStoreWorkflowPayloadResolver(codec, new JsonDaprSerializer());
        using var source = new MemoryStream(new byte[] { 1, 2, 3 });
        var token = TestContext.Current.CancellationToken;
        await Assert.ThrowsAnyAsync<ArgumentException>(() => resolver.StoreStreamAsync(source, "instance", token));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => resolver.UploadAsync(source, "instance", token));
        Assert.Equal(0, source.Position);
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FactoryFailureIsNotReplacedByDefaultRouting()
    {
        var failure = new InvalidOperationException("Tenant is unknown.");
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        var codec = new BinaryStorePayloadConverter(store.Object, Options.Create(new BinaryStorePayloadOptions
        {
            StoreName = "store", PrefixFactory = _ => throw failure
        }), NullWorkflowPayloadManifest.Instance);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => codec.WriteAsync(
            (_, _) => throw new Exception("Producer must not start."), "instance", TestContext.Current.CancellationToken)));
        store.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("normal-123", "normal-123")]
    [InlineData("tenant/instance", "tenant%2Finstance")]
    [InlineData("tenant\\instance", "tenant%5Cinstance")]
    [InlineData("tenant%2Finstance", "tenant%252Finstance")]
    [InlineData(".", "%2E")]
    [InlineData("..", "%2E%2E")]
    public void InstanceIdIsAlwaysOneUnambiguousSegment(string id, string expected)
    {
        var options = new BinaryStorePayloadOptions { StoreName = "store" };
        Assert.Equal($"workflow-payloads/{expected}", options.GetInstancePrefix(id));
        options.PrefixFactory = _ => "tenants/acme";
        Assert.Equal($"workflow-payloads/tenants/acme/{expected}", options.GetInstancePrefix(id));
    }

    [Fact]
    public async Task ExistingReferencesResolveAndCleanUpWithoutReevaluatingRouting()
    {
        var token = TestContext.Current.CancellationToken;
        var bytes = Encoding.UTF8.GetBytes("\"hello\"");
        var reference = new PayloadReference("old-store", "old/prefix/key", bytes.Length,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        async IAsyncEnumerable<ReadOnlyMemory<byte>> Download()
        {
            await Task.Yield();
            yield return bytes;
        }
        var store = new Mock<DaprBinaryStoreClient>(MockBehavior.Strict);
        store.Setup(s => s.GetBinaryFileChunksAsync("old-store", reference.Key, token)).Returns(Download);
        store.Setup(s => s.DeleteBinaryFileAsync("old-store", reference.Key, token)).Returns(Task.CompletedTask);
        var options = new BinaryStorePayloadOptions
        {
            StoreName = "new-store", KeyPrefix = "new-prefix",
            PrefixFactory = _ => throw new Exception("Reads and deletes must use recorded keys.")
        };
        var manifest = new Mock<IWorkflowPayloadManifest>(MockBehavior.Strict);
        manifest.Setup(m => m.ListAsync("instance", token)).ReturnsAsync(new[] { reference });
        manifest.Setup(m => m.ClearAsync("instance", token)).Returns(Task.CompletedTask);
        var codec = new BinaryStorePayloadConverter(store.Object, Options.Create(options), manifest.Object);
        IWorkflowPayloadResolver resolver = new BinaryStoreWorkflowPayloadResolver(codec, new JsonDaprSerializer());
        Assert.Equal(reference.ToEnvelope(), await codec.EncodeAsync(reference.ToEnvelope(),
            new PayloadSite(PayloadKind.ActivityOutput, "instance", null), token));
        Assert.Equal("hello", await resolver.ResolveAsync(reference.AsTyped<string>(), token));
        Assert.Equal(1, await new WorkflowPayloadCleaner(store.Object, manifest.Object, codec).PurgeAsync("instance", token));
        store.VerifyAll();
        manifest.VerifyAll();
    }
}
