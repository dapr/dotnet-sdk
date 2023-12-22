using Dapr.Client;

namespace Cryptography.Examples
{
    // This isn't yet implemented in the API, so it cannot yet be tested
    //    internal class SignVerifyExample : Example
    //    {
    //        public override string DisplayName => "Using Cryptography to sign a digest and verify that signature";

    //        public override async Task RunAsync(CancellationToken cancellationToken)
    //        {
    //            using var client = new DaprClientBuilder().Build();

    //            const string componentName = "azurekeyvault";
    //            const string keyName = "mykey"; // Change this to match the name of the key in your Vault
    //            const string algorithm = "RSA"; //The algorithm should match the key being used

    //            var digestBytes = "This is our starting value we'll build the signature for"u8.ToArray();

    //#pragma warning disable CS0618 // Type or member is obsolete
    //            var signature = await client.SignAsync(componentName, digestBytes, algorithm, keyName, cancellationToken);
    //#pragma warning restore CS0618 // Type or member is obsolete
    //            Console.WriteLine($"Signature: '{Convert.ToBase64String(signature)}'");

    //#pragma warning disable CS0618 // Type or member is obsolete
    //            var verification = await client.VerifyAsync(componentName, digestBytes, signature, algorithm, keyName,
    //                cancellationToken);
    //#pragma warning restore CS0618 // Type or member is obsolete
    //            Console.WriteLine($"Verification: {(verification ? "Success": "Failed")}");
    //        }
    //    }
}
