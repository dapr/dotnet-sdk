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
// ------------------------------------------------------------------------

#pragma warning disable DAPR_CRYPTOGRAPHY

using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using Dapr.Cryptography.Encryption;
using Dapr.Cryptography.Encryption.Extensions;
using Dapr.Cryptography.Encryption.Models;
using Dapr.Testcontainers;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Harnesses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Cryptography;

public sealed class CryptographyTests
{
    private const string KeyName = "rsa-private-key.pem";
    private const string ComponentName = Constants.DaprComponentNames.CryptographyComponentName;

    /// <summary>
    /// Copies the bundled PEM key into a <c>keys/</c> subdirectory of the components directory
    /// (which is bind-mounted into the Daprd container at <c>/components</c>) and returns the
    /// in-container path that the local-storage crypto component should use.
    /// </summary>
    private static string PrepareKeys(string componentsDir)
    {
        var keysDir = Path.Combine(componentsDir, "keys");
        Directory.CreateDirectory(keysDir);
        var sourceKey = Path.Combine(AppContext.BaseDirectory, "keys", KeyName);
        File.Copy(sourceKey, Path.Combine(keysDir, KeyName));
        return "/components/keys";
    }

    [Fact]
    public async Task ShouldEncryptAndDecryptString()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("crypto-components");
        var containerKeyPath = PrepareKeys(componentsDir);
        const string plaintext = "The quick brown fox jumps over the lazy dog";

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);

        var harness = new DaprHarnessBuilder(componentsDir).BuildCryptography(containerKeyPath);
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprEncryptionClient((sp, clientBuilder) =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                    if (!string.IsNullOrEmpty(grpcEndpoint))
                        clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprEncryptionClient>();

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext).AsMemory();
        var encrypted = await client.EncryptAsync(ComponentName, plaintextBytes, KeyName,
            new EncryptionOptions(KeyWrapAlgorithm.Rsa), TestContext.Current.CancellationToken);
        Assert.False(encrypted.IsEmpty);

        var decrypted = await client.DecryptAsync(ComponentName, encrypted, KeyName,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(plaintext, Encoding.UTF8.GetString(decrypted.Span));
    }

    [Fact]
    public async Task ShouldEncryptAndDecryptSmallFile()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("crypto-components");
        var containerKeyPath = PrepareKeys(componentsDir);
        const string fileContent = "Hello from Dapr Cryptography integration tests!";

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(cancellationToken: TestContext.Current.CancellationToken);
        await environment.StartAsync(TestContext.Current.CancellationToken);

        var harness = new DaprHarnessBuilder(componentsDir).BuildCryptography(containerKeyPath);
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprEncryptionClient((sp, clientBuilder) =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                    if (!string.IsNullOrEmpty(grpcEndpoint))
                        clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprEncryptionClient>();

        // Encrypt the file content using the streaming overload, buffering all chunks in memory.
        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));
        var encryptedBuffer = new ArrayBufferWriter<byte>();
        await foreach (var chunk in client.EncryptAsync(ComponentName, inputStream, KeyName,
                           new EncryptionOptions(KeyWrapAlgorithm.Rsa), TestContext.Current.CancellationToken))
        {
            encryptedBuffer.Write(chunk.Span);
        }

        Assert.True(encryptedBuffer.WrittenCount > 0);

        // Decrypt using the streaming overload, collecting all chunks back into a buffer.
        using var encryptedStream = new MemoryStream(encryptedBuffer.WrittenMemory.ToArray());
        var decryptedBuffer = new ArrayBufferWriter<byte>();
        await foreach (var chunk in client.DecryptAsync(ComponentName, encryptedStream, KeyName,
                           cancellationToken: TestContext.Current.CancellationToken))
        {
            decryptedBuffer.Write(chunk.Span);
        }

        Assert.Equal(fileContent, Encoding.UTF8.GetString(decryptedBuffer.WrittenSpan));
    }

    [Fact]
    public async Task ShouldEncryptAndDecryptLargeTempFile()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("crypto-components");
        var containerKeyPath = PrepareKeys(componentsDir);

        var sourceTempFile = Path.GetTempFileName();
        var encryptedTempFile = Path.GetTempFileName();
        var decryptedTempFile = Path.GetTempFileName();

        try
        {
            // Create a 5 MB temp file filled with random bytes and record its MD5.
            const int fileSize = 5 * 1024 * 1024;
            var randomBytes = new byte[fileSize];
            Random.Shared.NextBytes(randomBytes);
            await File.WriteAllBytesAsync(sourceTempFile, randomBytes, TestContext.Current.CancellationToken);
            var sourceHash = MD5.HashData(randomBytes);

            await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(cancellationToken: TestContext.Current.CancellationToken);
            await environment.StartAsync(TestContext.Current.CancellationToken);

            var harness = new DaprHarnessBuilder(componentsDir).BuildCryptography(containerKeyPath);
            await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
                .ConfigureServices(builder =>
                {
                    builder.Services.AddDaprEncryptionClient((sp, clientBuilder) =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                        if (!string.IsNullOrEmpty(grpcEndpoint))
                            clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                    });
                })
                .BuildAndStartAsync();

            using var scope = testApp.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<DaprEncryptionClient>();

            // Stream-encrypt the source file to a second temp file.
            {
                await using var sourceStream = new FileStream(sourceTempFile, FileMode.Open, FileAccess.Read);
                await using var encStream = new FileStream(encryptedTempFile, FileMode.Create, FileAccess.Write);
                await foreach (var chunk in client.EncryptAsync(ComponentName, sourceStream, KeyName,
                                   new EncryptionOptions(KeyWrapAlgorithm.Rsa), TestContext.Current.CancellationToken))
                {
                    await encStream.WriteAsync(chunk, TestContext.Current.CancellationToken);
                }
            }

            Assert.True(new FileInfo(encryptedTempFile).Length > 0);

            // Stream-decrypt the encrypted file to a third temp file.
            {
                await using var encStream = new FileStream(encryptedTempFile, FileMode.Open, FileAccess.Read);
                await using var decStream = new FileStream(decryptedTempFile, FileMode.Create, FileAccess.Write);
                await foreach (var chunk in client.DecryptAsync(ComponentName, encStream, KeyName,
                                   cancellationToken: TestContext.Current.CancellationToken))
                {
                    await decStream.WriteAsync(chunk, TestContext.Current.CancellationToken);
                }
            }

            // Verify round-trip integrity via MD5.
            var decryptedBytes = await File.ReadAllBytesAsync(decryptedTempFile, TestContext.Current.CancellationToken);
            Assert.Equal(sourceHash, MD5.HashData(decryptedBytes));
        }
        finally
        {
            File.Delete(sourceTempFile);
            File.Delete(encryptedTempFile);
            File.Delete(decryptedTempFile);
        }
    }
}

#pragma warning restore DAPR_CRYPTOGRAPHY
