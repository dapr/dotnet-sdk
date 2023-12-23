using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
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
                Array.Empty<byte>(), KeyWrapAlgorithm.Rsa, "MyKey", DataEncryptionCipher.AesGcm,
                CancellationToken.None));
        }

        [Fact]
        public async Task EncryptAsync_ByteArray_KeyName_ArgumentVerifierException()
        {
            var client = new DaprClientBuilder().Build();
            const string keyName = "";
            //Get response and validate
            await Assert.ThrowsAsync<ArgumentException>(async () => await client.EncryptAsync("myVault",
                Array.Empty<byte>(), KeyWrapAlgorithm.Rsa, keyName, DataEncryptionCipher.AesGcm,
                CancellationToken.None));
        }

        [Fact]
        public async Task EncryptAsync_Stream_VaultResourceName_ArgumentVerifierException()
        {
            var client = new DaprClientBuilder().Build();
            const string vaultResourceName = "";
            //Get response and validate
            await Assert.ThrowsAsync<ArgumentException>(async () => await client.EncryptAsync(vaultResourceName,
                new MemoryStream(), KeyWrapAlgorithm.Rsa, "MyKey", DataEncryptionCipher.AesGcm,
                CancellationToken.None));
        }

        [Fact]
        public async Task EncryptAsync_Stream_KeyName_ArgumentVerifierException()
        {
            var client = new DaprClientBuilder().Build();
            const string keyName = "";
            //Get response and validate
            await Assert.ThrowsAsync<ArgumentException>(async () => await client.EncryptAsync("myVault",
                new MemoryStream(), KeyWrapAlgorithm.Rsa, keyName, DataEncryptionCipher.AesGcm,
                CancellationToken.None));
        }

        [Fact]
        public async Task DecryptAsync_ByteArray_VaultResourceName_ArgumentVerifierException()
        {
            var client = new DaprClientBuilder().Build();
            const string vaultResourceName = "";
            //Get response and validate
            await Assert.ThrowsAsync<ArgumentException>(async () => await client.DecryptAsync(vaultResourceName,
                Array.Empty<byte>(), "myKey", CancellationToken.None));
        }

        [Fact]
        public async Task DecryptAsync_ByteArray_KeyName_ArgumentVerifierException()
        {
            var client = new DaprClientBuilder().Build();
            const string keyName = "";
            //Get response and validate
            await Assert.ThrowsAsync<ArgumentException>(async () => await client.DecryptAsync("myVault",
                Array.Empty<byte>(), keyName, CancellationToken.None));
        }

        [Fact]
        public async Task DecryptAsync_Stream_VaultResourceName_ArgumentVerifierException()
        {
            var client = new DaprClientBuilder().Build();
            const string vaultResourceName = "";
            //Get response and validate
            await Assert.ThrowsAsync<ArgumentException>(async () => await client.DecryptAsync(vaultResourceName,
                new MemoryStream(), "MyKey", CancellationToken.None));
        }

        [Fact]
        public async Task DecryptAsync_Stream_KeyName_ArgumentVerifierException()
        {
            var client = new DaprClientBuilder().Build();
            const string keyName = "";
            //Get response and validate
            await Assert.ThrowsAsync<ArgumentException>(async () => await client.DecryptAsync("myVault",
                new MemoryStream(), keyName, CancellationToken.None));
        }
    }
}
