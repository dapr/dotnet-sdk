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

using System.Buffers;
using Dapr.Client;
#pragma warning disable CS0618 // Type or member is obsolete

namespace Cryptography.Examples
{
    internal class EncryptDecryptFileStreamExample(string componentName, string keyName) : Example
    {
        public override string DisplayName => "Use Cryptography to encrypt and decrypt a file";
        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            using var client = new DaprClientBuilder().Build();

            // The name of the file we're using as an example
            const string fileName = "file.txt";

            Console.WriteLine("Original file contents:");
            foreach (var line in await File.ReadAllLinesAsync(fileName, cancellationToken))
            {
                Console.WriteLine(line);
            }

            //Encrypt from a file stream and buffer the resulting bytes to an in-memory buffer
            await using var encryptFs = new FileStream(fileName, FileMode.Open);

            var bufferedEncryptedBytes = new ArrayBufferWriter<byte>();
            await foreach (var bytes in (await client.EncryptAsync(componentName, encryptFs, keyName,
                               new EncryptionOptions(KeyWrapAlgorithm.Rsa), cancellationToken))
                           .WithCancellation(cancellationToken))
            {
                bufferedEncryptedBytes.Write(bytes.Span);
            }

            Console.WriteLine("Encrypted bytes:");
            Console.WriteLine(Convert.ToBase64String(bufferedEncryptedBytes.WrittenMemory.ToArray()));
            
            //We'll write to a temporary file via a FileStream
            var tempDecryptedFile = Path.GetTempFileName();
            await using var decryptFs = new FileStream(tempDecryptedFile, FileMode.Create);
            
            //We'll stream the decrypted bytes from a MemoryStream into the above temporary file
            await using var encryptedMs = new MemoryStream(bufferedEncryptedBytes.WrittenMemory.ToArray());
            await foreach (var result in (await client.DecryptAsync(componentName, encryptedMs, keyName,
                               cancellationToken)).WithCancellation(cancellationToken))
            {
                decryptFs.Write(result.Span);
            }

            decryptFs.Close();
            
            //Let's confirm the value as written to the file
            var decryptedValue = await File.ReadAllTextAsync(tempDecryptedFile, cancellationToken);
            Console.WriteLine("Decrypted value: ");
            Console.WriteLine(decryptedValue);
            
            //And some cleanup to delete our temp file
            File.Delete(tempDecryptedFile);
        }
    }
}
