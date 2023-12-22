using System.Security.Cryptography;
using System.Text;
using Dapr.Client;

namespace Cryptography.Examples
{
    // This isn't yet implemented in the API, so it cannot yet be tested
    //    internal class WrapUnwrapKeyExample : Example
    //    {
    //        public override string DisplayName => "Using Cryptography to retrieve, wrap and unwrap a given key";

    //        public override async Task RunAsync(CancellationToken cancellationToken)
    //        {
    //            using var client = new DaprClientBuilder().Build();

    //            const string componentName = "azurekeyvault"; // Change this to match the name of the component containing your vault
    //            const string keyName = "mykey"; // Change this to match the name of the key in your Vault

    //            var nonceBytes = "This is a nonce"u8.ToArray();

    //            //Generate a new private key for our purposes here
    //            var privateKeyBytes = new List<byte>();
    //            using (var rsa = new RSACryptoServiceProvider(2048))
    //            {
    //                try
    //                {
    //                    var privateKey = rsa.ExportEncryptedPkcs8PrivateKey("password",
    //                        new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 5));

    //                    privateKeyBytes.AddRange(privateKey);
    //                }
    //                finally
    //                {
    //                    rsa.PersistKeyInCsp = false;
    //                }
    //            }

    //            //Wrap the key
    //            var wrappedKeyResult =
    //#pragma warning disable CS0618 // Type or member is obsolete
    //                await client.WrapKeyAsync(componentName, privateKeyBytes.ToArray(), keyName, "RSA", nonceBytes, cancellationToken);
    //#pragma warning restore CS0618 // Type or member is obsolete
    //            Console.WriteLine($"Wrapped key bytes: '{Convert.ToBase64String(wrappedKeyResult.WrappedKey)}'");

    //            //Unwrap the key
    //#pragma warning disable CS0618 // Type or member is obsolete
    //            var unwrappedKey = await client.UnwrapKeyAsync(componentName, wrappedKeyResult.WrappedKey, "RSA", keyName,
    //                nonceBytes, Array.Empty<byte>(), cancellationToken);
    //#pragma warning restore CS0618 // Type or member is obsolete
    //            Console.WriteLine($"Unwrapped key value:");
    //        }
    //    }
}
