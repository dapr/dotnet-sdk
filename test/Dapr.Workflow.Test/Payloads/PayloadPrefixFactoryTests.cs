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
    [Theory]
    [InlineData("automatic")]
    [InlineData("automatic-stream")]
    [InlineData("value")]
    [InlineData("staged")]
    [InlineData("stream")]
    [InlineData("chunks")]
    [InlineData("producer")]
    public async Task EveryWriteUsesFactoryAndPreservesInstanceOwnership(string mode)
    {
        var token = TestContext.Current.CancellationToken;
        var calls = new List<string>();
        var options = new BinaryStorePayloadOptions
        {
            StoreName = "store", KeyPrefix = "root",
            WritePreference = PayloadWritePreference.PreferReference, OffloadThreshold = null,
            InstancePrefixFactory = id =>
            {
                calls.Add(id);
                return $"tenants/{id.Split('-')[0]}/instances";
            }
        };
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
        foreach (var id in new[] { "acme-1", "acme-2", "bravo-1" })
        {
            using var source = new MemoryStream(bytes);
            async IAsyncEnumerable<ReadOnlyMemory<byte>> Chunks()
            {
                await Task.Yield();
                yield return bytes;
            }
            var reference = mode switch
            {
                "automatic" => PayloadReferenceParser.Parse(await codec.EncodeAsync("\"hello\"",
                    new PayloadSite(PayloadKind.ActivityOutput, id, null), token))!,
                "automatic-stream" => PayloadReferenceParser.Parse(await codec.EncodeStreamAsync(source,
                    new PayloadSite(PayloadKind.WorkflowInput, id, null), token))!,
                "value" => await resolver.StoreAsync("hello", id, token),
                "staged" => await resolver.StoreStreamAsync(source, id, token),
                "stream" => await resolver.UploadAsync(source, id, token),
                "chunks" => await resolver.UploadAsync(Chunks(), id, token),
                _ => await resolver.UploadAsync((destination, ct) => destination.WriteAsync(bytes, ct).AsTask(), id, token)
            };
            var prefix = $"root/tenants/{id.Split('-')[0]}/instances/{id}/";
            Assert.StartsWith(prefix, reference.Key);
            Assert.Equal(hash, reference.ContentHash);
            Assert.Equal(bytes.Length, reference.Size);
            if (mode is "stream" or "chunks" or "producer")
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
            StoreName = "store", InstancePrefixFactory = _ => prefix!
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
            StoreName = "store", InstancePrefixFactory = _ => throw failure
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
        options.InstancePrefixFactory = _ => "tenants/acme";
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
            InstancePrefixFactory = _ => throw new Exception("Reads and deletes must use recorded keys.")
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
