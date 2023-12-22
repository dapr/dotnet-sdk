using System.Text;
using Dapr.Client;

namespace Cryptography.Examples
{
    internal class EncryptDecryptExample : Example
    {
        public override string DisplayName => "Using Cryptography to encrypt and decrypt a string";

        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            using var client = new DaprClientBuilder().Build();

            const string componentName = "azurekeyvault"; //Change this to match the name of the component containing your vault
            const string keyName = "myKey"; //Change this to match the name of the key in your Vault
            const string algorithm = "RSA"; //The algorithm used should match the type of key used.

            var nonceBytes = "This in our nonce value"u8.ToArray();

            const string plaintextStr = "This is the value we're going to encrypt today";
            Console.WriteLine($"Original string value: '{plaintextStr}'");

            //Encrypt the string
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintextStr);
#pragma warning disable CS0618 // Type or member is obsolete
            var encryptedBytesResult = await client.EncryptAsync(componentName, plaintextBytes, algorithm, keyName,
                nonceBytes, cancellationToken);
#pragma warning restore CS0618 // Type or member is obsolete

            //Decrypt the string
#pragma warning disable CS0618 // Type or member is obsolete
            var decryptedBytes = await client.DecryptAsync(componentName, encryptedBytesResult.CipherTextBytes,
                algorithm, keyName,
                nonceBytes, Array.Empty<byte>(), cancellationToken);
#pragma warning restore CS0618 // Type or member is obsolete
            Console.WriteLine($"Decrypted string: '{Encoding.UTF8.GetString(decryptedBytes)}'");
        }
    }
}
