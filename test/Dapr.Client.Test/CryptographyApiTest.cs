using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
#pragma warning disable CS0618 // Type or member is obsolete

namespace Dapr.Client.Test
{
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
    }
}
