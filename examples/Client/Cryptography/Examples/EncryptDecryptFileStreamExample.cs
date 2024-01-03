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

using Dapr.Client;

namespace Cryptography.Examples
{
    internal class EncryptDecryptFileStreamExample : Example
    {
        public override string DisplayName => "Use Cryptography to encrypt and decrypt a file";
        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            using var client = new DaprClientBuilder().Build();

            const string componentName = "azurekeyvault"; // Change this to match the name of the component containing your vault
            const string keyName = "myKey";

            // The name of the file we're using as an example
            const string fileName = "file.txt";

            Console.WriteLine("Original file contents:");
            foreach (var line in await File.ReadAllLinesAsync(fileName, cancellationToken))
            {
                Console.WriteLine(line);
            }
            Console.WriteLine();

            //Encrypt the file
            await using var encryptFs = new FileStream(fileName, FileMode.Open);
#pragma warning disable CS0618 // Type or member is obsolete
            var encryptedBytesResult = await client.EncryptAsync(componentName, encryptFs, keyName, new EncryptionOptions(KeyWrapAlgorithm.Rsa)
            {
                EncryptionCipher = DataEncryptionCipher.AesGcm
            }, cancellationToken);
#pragma warning restore CS0618 // Type or member is obsolete
            Console.WriteLine($"Encrypted bytes: '{Convert.ToBase64String(encryptedBytesResult.Span)}'");
            Console.WriteLine();

            //Decrypt the temp file from a memory stream this time instead of a file
            await using var ms = new MemoryStream(encryptedBytesResult.ToArray());
#pragma warning disable CS0618 // Type or member is obsolete
            var decryptedBytes = await client.DecryptAsync(componentName, ms, keyName, new DecryptionOptions(), cancellationToken);
#pragma warning restore CS0618 // Type or member is obsolete

            Console.WriteLine("Decrypted value:");
            await using var decryptedMs = new MemoryStream(decryptedBytes.ToArray());
            using var sr = new StreamReader(decryptedMs);
            Console.WriteLine(await sr.ReadToEndAsync(cancellationToken));
        }
    }
}
