// ------------------------------------------------------------------------
// Copyright 2023 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

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
            
            
            const string plaintextStr = "This is the value we're going to encrypt today";
            Console.WriteLine($"Original string value: '{plaintextStr}'");

            //Encrypt the string
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintextStr);
#pragma warning disable CS0618 // Type or member is obsolete
            var encryptedBytesResult = await client.EncryptAsync(componentName, plaintextBytes, KeyWrapAlgorithm.Rsa, keyName, DataEncryptionCipher.AesGcm,
                cancellationToken);
#pragma warning restore CS0618 // Type or member is obsolete

            Console.WriteLine($"Encrypted bytes: '{Convert.ToBase64String(encryptedBytesResult)}'");

            //Decrypt the string
#pragma warning disable CS0618 // Type or member is obsolete
            var decryptedBytes = await client.DecryptAsync(componentName, encryptedBytesResult, keyName, cancellationToken);
#pragma warning restore CS0618 // Type or member is obsolete
            Console.WriteLine($"Decrypted string: '{Encoding.UTF8.GetString(decryptedBytes)}'");
        }
    }
}
