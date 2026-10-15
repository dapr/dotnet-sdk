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

using Dapr.BinaryStore;
using Dapr.BinaryStore.Extensions;
using Dapr.Testcontainers;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Testing;
using Dapr.Testcontainers.Harnesses;
using Dapr.Testcontainers.Xunit.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.BinaryStore;

public sealed class BinaryStoreTests
{
    private const string StoreName = Constants.DaprComponentNames.BinaryStoreComponentName;

    private static async Task<(DaprTestEnvironment Environment, DaprTestApplication App)> StartAsync(
        long? maxSize = null, long? maxTotalSize = null)
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("binarystore-components");
        var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);

        var harness = new DaprHarnessBuilder(componentsDir).BuildBinaryStore(maxSize, maxTotalSize);
        var app = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprBinaryStoreClient((sp, clientBuilder) =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                    if (!string.IsNullOrEmpty(grpcEndpoint))
                        clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                });
            })
            .BuildAndStartAsync();
        return (environment, app);
    }

    private static byte[] RandomBytes(int length)
    {
        var bytes = new byte[length];
        new Random(42).NextBytes(bytes);
        return bytes;
    }

    [MinimumDaprRuntimeFact("1.19")]
    public async Task ShouldRoundTripSmallFile()
    {
        var (environment, testApp) = await StartAsync();
        await using var _ = environment;
        await using var __ = testApp;
        using var scope = testApp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprBinaryStoreClient>();
        var ct = TestContext.Current.CancellationToken;

        var data = RandomBytes(1024);
        await client.SetBinaryFileAsync(StoreName, "small.bin", new ReadOnlyMemory<byte>(data), cancellationToken: ct);

        var result = await client.GetBinaryFileBytesAsync(StoreName, "small.bin", ct);
        Assert.Equal(data, result);
    }

    [MinimumDaprRuntimeFact("1.19")]
    public async Task ShouldRoundTripMultiChunkFileInNestedPath()
    {
        var (environment, testApp) = await StartAsync();
        await using var _ = environment;
        await using var __ = testApp;
        using var scope = testApp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprBinaryStoreClient>();
        var ct = TestContext.Current.CancellationToken;

        var data = RandomBytes(1024 * 1024 + 123);
        await client.SetBinaryFileAsync(StoreName, "dir/nested/large.bin", new MemoryStream(data), chunkSize: 16 * 1024, cancellationToken: ct);

        using var destination = new MemoryStream();
        await client.GetBinaryFileAsync(StoreName, "dir/nested/large.bin", destination, ct);
        Assert.Equal(data, destination.ToArray());
    }

    [MinimumDaprRuntimeFact("1.19")]
    public async Task ShouldRejectExistingFileWithoutOverwriteAndReplaceWithOverwrite()
    {
        var (environment, testApp) = await StartAsync();
        await using var _ = environment;
        await using var __ = testApp;
        using var scope = testApp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprBinaryStoreClient>();
        var ct = TestContext.Current.CancellationToken;

        await client.SetBinaryFileAsync(StoreName, "ow.bin", new ReadOnlyMemory<byte>([1, 2, 3]), cancellationToken: ct);
        await Assert.ThrowsAsync<DaprException>(() =>
            client.SetBinaryFileAsync(StoreName, "ow.bin", new ReadOnlyMemory<byte>([9]), overwrite: false, cancellationToken: ct));

        await client.SetBinaryFileAsync(StoreName, "ow.bin", new ReadOnlyMemory<byte>([4, 5]), overwrite: true, cancellationToken: ct);
        Assert.Equal(new byte[] { 4, 5 }, await client.GetBinaryFileBytesAsync(StoreName, "ow.bin", ct));
    }

    [MinimumDaprRuntimeFact("1.19")]
    public async Task ShouldDeleteFile()
    {
        var (environment, testApp) = await StartAsync();
        await using var _ = environment;
        await using var __ = testApp;
        using var scope = testApp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprBinaryStoreClient>();
        var ct = TestContext.Current.CancellationToken;

        await client.SetBinaryFileAsync(StoreName, "del.bin", new ReadOnlyMemory<byte>([1]), cancellationToken: ct);
        await client.DeleteBinaryFileAsync(StoreName, "del.bin", ct);

        await Assert.ThrowsAsync<DaprException>(() => client.GetBinaryFileBytesAsync(StoreName, "del.bin", ct));
    }

    [MinimumDaprRuntimeFact("1.19")]
    public async Task ShouldFailToGetMissingFile()
    {
        var (environment, testApp) = await StartAsync();
        await using var _ = environment;
        await using var __ = testApp;
        using var scope = testApp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprBinaryStoreClient>();

        await Assert.ThrowsAsync<DaprException>(() =>
            client.GetBinaryFileBytesAsync(StoreName, "missing.bin", TestContext.Current.CancellationToken));
    }

    [MinimumDaprRuntimeTheory("1.19")]
    [InlineData("/leading.bin")]
    [InlineData("trailing/")]
    [InlineData("a//b.bin")]
    [InlineData("a/../b.bin")]
    [InlineData("star*.bin")]
    public async Task ShouldRejectInvalidFileNames(string fileName)
    {
        var (environment, testApp) = await StartAsync();
        await using var _ = environment;
        await using var __ = testApp;
        using var scope = testApp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprBinaryStoreClient>();

        await Assert.ThrowsAsync<DaprException>(() =>
            client.SetBinaryFileAsync(StoreName, fileName, new ReadOnlyMemory<byte>([1]),
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [MinimumDaprRuntimeFact("1.19")]
    public async Task ShouldRejectFileLargerThanMaxSize()
    {
        var (environment, testApp) = await StartAsync(maxSize: 1024, maxTotalSize: 4096);
        await using var _ = environment;
        await using var __ = testApp;
        using var scope = testApp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprBinaryStoreClient>();

        await Assert.ThrowsAsync<DaprException>(() =>
            client.SetBinaryFileAsync(StoreName, "big.bin", new ReadOnlyMemory<byte>(RandomBytes(8 * 1024)),
                cancellationToken: TestContext.Current.CancellationToken));
    }
}
