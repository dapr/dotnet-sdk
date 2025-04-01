using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
#pragma warning disable CS0618 // Type or member is obsolete

namespace Dapr.Client.Test;

public class CryptographyApiTest
{
    [Fact]
    public async Task EncryptAsync_ByteArray_VaultResourceName_ArgumentVerifierException()
    {
        var client = new DaprClientBuilder().Build();
        const string vaultResourceName = "";
        //Get response and validate
        await Assert.ThrowsAsync<ArgumentException>(async () => await client.EncryptAsync(vaultResourceName,
            (ReadOnlyMemory<byte>)Array.Empty<byte>(), "MyKey", new EncryptionOptions(KeyWrapAlgorithm.Rsa),
            CancellationToken.None));
    }

    [Fact]
    public async Task EncryptAsync_ByteArray_KeyName_ArgumentVerifierException()
    {
        var client = new DaprClientBuilder().Build();
        const string keyName = "";
        //Get response and validate
        await Assert.ThrowsAsync<ArgumentException>(async () => await client.EncryptAsync( "myVault",
            (ReadOnlyMemory<byte>) Array.Empty<byte>(), keyName, new EncryptionOptions(KeyWrapAlgorithm.Rsa), CancellationToken.None));
    }

    [Fact]
    public async Task EncryptAsync_Stream_VaultResourceName_ArgumentVerifierException()
    {
        var client = new DaprClientBuilder().Build();
        const string vaultResourceName = "";
        //Get response and validate
        await Assert.ThrowsAsync<ArgumentException>(async () => await client.EncryptAsync(vaultResourceName,
            new MemoryStream(), "MyKey", new EncryptionOptions(KeyWrapAlgorithm.Rsa),
            CancellationToken.None));
    }

    [Fact]
    public async Task EncryptAsync_Stream_KeyName_ArgumentVerifierException()
    {
        var client = new DaprClientBuilder().Build();
        const string keyName = "";
        //Get response and validate
        await Assert.ThrowsAsync<ArgumentException>(async () => await client.EncryptAsync("myVault",
            (Stream) new MemoryStream(), keyName, new EncryptionOptions(KeyWrapAlgorithm.Rsa),
            CancellationToken.None));
    }

    [Fact]
    public async Task DecryptAsync_ByteArray_VaultResourceName_ArgumentVerifierException()
    {
        var client = new DaprClientBuilder().Build();
        const string vaultResourceName = "";
        //Get response and validate
        await Assert.ThrowsAsync<ArgumentException>(async () => await client.DecryptAsync(vaultResourceName,
            Array.Empty<byte>(), "myKey", new DecryptionOptions(), CancellationToken.None));
    }

    [Fact]
    public async Task DecryptAsync_ByteArray_KeyName_ArgumentVerifierException()
    {
        var client = new DaprClientBuilder().Build();
        const string keyName = "";
        //Get response and validate
        await Assert.ThrowsAsync<ArgumentException>(async () => await client.DecryptAsync("myVault",
            Array.Empty<byte>(), keyName, new DecryptionOptions(), CancellationToken.None));
    }

    [Fact]
    public async Task DecryptAsync_Stream_VaultResourceName_ArgumentVerifierException()
    {
        var client = new DaprClientBuilder().Build();
        const string vaultResourceName = "";
        //Get response and validate
        await Assert.ThrowsAsync<ArgumentException>(async () => await client.DecryptAsync(vaultResourceName,
            new MemoryStream(), "MyKey", new DecryptionOptions(), CancellationToken.None));
    }

    [Fact]
    public async Task DecryptAsync_Stream_KeyName_ArgumentVerifierException()
    {
        var client = new DaprClientBuilder().Build();
        const string keyName = "";
        //Get response and validate
        await Assert.ThrowsAsync<ArgumentException>(async () => await client.DecryptAsync("myVault",
            new MemoryStream(), keyName, new DecryptionOptions(), CancellationToken.None));
    }
}