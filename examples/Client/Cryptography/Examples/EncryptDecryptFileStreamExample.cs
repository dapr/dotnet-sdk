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
#pragma warning disable CS0618 // Type or member is obsolete

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

            //Encrypt from a file stream and buffer the resulting bytes to an in-memory List<byte>
            await using var encryptFs = new FileStream(fileName, FileMode.Open);
            var bufferedEncBytes = new List<byte>();
            await foreach (var bytes in client.EncryptAsync(componentName, encryptFs, keyName,
                               new EncryptionOptions(KeyWrapAlgorithm.Rsa), cancellationToken))
            {
                bufferedEncBytes.AddRange(bytes);
            }
            
            var encryptedBytesArr = bufferedEncBytes.ToArray();
            Console.WriteLine($"Encrypted bytes: {Convert.ToBase64String(encryptedBytesArr)}");
            Console.WriteLine();
            
            //Decrypt the bytes from a memory stream back into a file (via stream)
            
            //We'll write to a temporary file via a FileStream
            var tempDecryptedFile = Path.GetTempFileName();
            await using var decryptFs = new FileStream(tempDecryptedFile, FileMode.Create);
            
            //We'll decrypt the bytes from a MemoryStream
            await using var encryptedMs = new MemoryStream(encryptedBytesArr);
            await foreach (var result in client.DecryptAsync(componentName, encryptedMs, keyName, cancellationToken))
            {
                decryptFs.Write(result);
            }
            decryptFs.Close();
            
            //Let's confirm the value as written to the file
            var decryptedValue = await File.ReadAllTextAsync(tempDecryptedFile, cancellationToken);
            Console.WriteLine($"Decrypted value: ");
            Console.WriteLine(decryptedValue);
            
            //And some cleanup to delete our temp file
            File.Delete(tempDecryptedFile);
        }
    }
}
