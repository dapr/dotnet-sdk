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

using System.ComponentModel;
using System.Security.Cryptography;
using Dapr.Cryptography.Encryption;

namespace CryptographySample;

public sealed class EncryptionOperation(DaprEncryptionClient encryptionClient) : IHostedService
{
    private const string VaultComponentName = "myvault";
    
    /// <summary>
    /// Triggered when the application host is ready to start the service.
    /// </summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    /// <returns>A <see cref="T:System.Threading.Tasks.Task" /> that represents the asynchronous Start operation.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        //Download a medium file to use for this demonstration
        var (filePath, checksum) = await DownloadMediumFileAsync();
        
        //Encrypt the file
        var encryptedBytes =  await encryptionClient.EncryptAsync()
        
        //Get the checksum of the encrypted file
        
        //Decrypt the file
        
        //Get the decrypted file's checksum
        
        
    }

    /// <summary>
    /// Triggered when the application host is performing a graceful shutdown.
    /// </summary>
    /// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
    /// <returns>A <see cref="T:System.Threading.Tasks.Task" /> that represents the asynchronous Stop operation.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    
    /// <summary>
    /// Downloads ZIP file of Dapr Docs repository - ~50MB.
    /// </summary>
    /// <returns></returns>
    private static async Task<(string filePath, string checksum)> DownloadMediumFileAsync()
    {
        const string fileName = "mediumFile.zip";
        using var httpClient = new HttpClient();
        var response =
            await httpClient.GetStreamAsync("https://github.com/dapr/docs/archive/refs/heads/master.zip");
        await using var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
        await response.CopyToAsync(fileStream);
        var checksum = await CalculateChecksum(fileStream.Name);
        return (fileStream.Name, checksum);
    }

    /// <summary>
    /// Calculates a checksum for a given file given its path.
    /// </summary>
    /// <param name="filePath">The path of the file to evaluate.</param>
    /// <returns></returns>
    private static async Task<string> CalculateChecksum(string filePath)
    {
        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(fs);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }
}


