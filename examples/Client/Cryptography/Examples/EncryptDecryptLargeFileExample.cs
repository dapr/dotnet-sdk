// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using System.Security.Cryptography;using Dapr.Crypto.Encryption;
using Dapr.Crypto.Encryption.Models;

#pragma warning disable CS0618 // Type or member is obsolete

namespace Cryptography.Examples;

internal sealed class EncryptDecryptLargeFileExample(DaprEncryptionClient daprClient) : IExample
{
    public static string DisplayName => "Use Cryptography to encrypt and decrypt a large file";
    
    public async Task RunAsync(string componentName, string keyName, CancellationToken cancellationToken)
    {
        //Create our large file locally and fill with random bytes
        const string fileName = "templargefile.txt";
        const long sizeLimitInBytes = 1L * 1024 * 1024 * 1024; //1 GB
        await WriteLargeFileAsync(fileName, sizeLimitInBytes);
        
        //Get the starting hash of the file for comparison reasons
        var startingFileHash = await GetFileHashAsync(fileName);
        var startingFileLength = new FileInfo(fileName).Length;
        Console.WriteLine($"Starting with a file spanning {startingFileLength} bytes called '{fileName}' filled with random bytes with MD5 hash of: '{startingFileHash}'");
        
        //Encrypt from the file stream and write the result to another file
        const string encryptedFileName = "enc_templargefile.txt";
        await using (var encryptFs = new FileStream(fileName, FileMode.Open))
        {
            await using (var encryptedFs = new FileStream(encryptedFileName, FileMode.Create))
            {
                await foreach (var encryptedBytes in ((daprClient.EncryptAsync(componentName, encryptFs, keyName,
                                   new EncryptionOptions(KeyWrapAlgorithm.Rsa), cancellationToken))))
                {
                    await encryptedFs.WriteAsync(encryptedBytes, cancellationToken);
                }

                encryptedFs.Close();
            }
        }

        //Get a hash and length of our newly encrypted file
        var encryptedFileHash = await GetFileHashAsync(encryptedFileName);
        var encryptedFileLength = new FileInfo(encryptedFileName).Length;
        Console.WriteLine($"Encrypted file spanning {encryptedFileLength} bytes called '{encryptedFileName}' with MD5 hash of: '{encryptedFileHash}'");
        
        //Now we'll decrypt the file back into another file
        const string decryptedFileName = "dec_templargefile.txt";
        await using (var decryptFs = new FileStream(encryptedFileName, FileMode.Open))
        {
            await using (var decryptedFs = new FileStream(decryptedFileName, FileMode.Create))
            {
                await foreach (var decryptedBytes in ((daprClient.DecryptAsync(componentName, decryptFs, keyName,
                                   cancellationToken: cancellationToken))))
                {
                    await decryptedFs.WriteAsync(decryptedBytes, cancellationToken);
                }

                decryptedFs.Close();
            }
        }

        //Get the hash and length of the decrypted file
        var decryptedFileHash = await GetFileHashAsync(decryptedFileName);
        var decryptedFileLength = new FileInfo(decryptedFileName).Length;
        Console.WriteLine($"Decrypted file spanning {decryptedFileLength} bytes called '{decryptedFileName}' with MD5 hash of: '{decryptedFileHash}'");

        var match = string.Equals(startingFileHash, decryptedFileHash);
        Console.WriteLine($"The hash of the original and decrypted file are {(match ? "": "NOT ")}the same!");

        //Clean up our large files
        File.Delete(fileName);
        File.Delete(encryptedFileName);
        File.Delete(decryptedFileName);
    }

    private static async Task WriteLargeFileAsync(string fileName, long sizeLimit)
    {
        var buffer = new byte[5 * 1024 * 1024]; // 5 MB buffer
        var random = new Random();

        await using var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Read);
        for (var written = 0; written < sizeLimit; written += buffer.Length)
        {
            random.NextBytes(buffer);
            await fs.WriteAsync(buffer);
        }
    }

    private static async Task<string> GetFileHashAsync(string fileName)
    {
        using var md5 = MD5.Create();
        await using var stream = File.OpenRead(fileName);
        return Convert.ToBase64String(await md5.ComputeHashAsync(stream));
    }
}
