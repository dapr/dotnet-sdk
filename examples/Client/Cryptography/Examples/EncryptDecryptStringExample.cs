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
#pragma warning disable CS0618 // Type or member is obsolete

namespace Cryptography.Examples;

internal class EncryptDecryptStringExample(string componentName, string keyName) : Example
{
    public override string DisplayName => "Using Cryptography to encrypt and decrypt a string";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        using var client = new DaprClientBuilder().Build();
            
        const string plaintextStr = "This is the value we're going to encrypt today";
        Console.WriteLine($"Original string value: '{plaintextStr}'");

        //Encrypt the string
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintextStr);
        var encryptedBytesResult = await client.EncryptAsync(componentName, plaintextBytes, keyName, new EncryptionOptions(KeyWrapAlgorithm.Rsa),
            cancellationToken);

        Console.WriteLine($"Encrypted bytes: '{Convert.ToBase64String(encryptedBytesResult.Span)}'");

        //Decrypt the string
        var decryptedBytes = await client.DecryptAsync(componentName, encryptedBytesResult, keyName, cancellationToken);
        Console.WriteLine($"Decrypted string: '{Encoding.UTF8.GetString(decryptedBytes.ToArray())}'");
    }
}