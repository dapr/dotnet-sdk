// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using System.Security.Cryptography;

namespace Dapr.Common.Data.Operations.Providers.Integrity;

/// <summary>
/// Provides a data integrity validation service using an SHA256 hash.
/// </summary>
public class Sha256Validator : IDaprDataValidator
{
    /// <summary>
    /// The name of the operation.
    /// </summary>
    public string Name => "Dapr.Integrity.Sha256";
    
    /// <summary>
    /// Executes the data processing operation. 
    /// </summary>
    /// <param name="input">The input data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The output data and metadata for the operation.</returns>
    public async Task<DaprOperationPayload<ReadOnlyMemory<byte>>> ExecuteAsync(ReadOnlyMemory<byte> input, CancellationToken cancellationToken = default)
    {
        var checksum = await CalculateChecksumAsync(input, cancellationToken);
        var result = new DaprOperationPayload<ReadOnlyMemory<byte>>(input);
        result.Metadata.Add(GetChecksumKey(), checksum);
        return result;
    }

    /// <summary>
    /// Reverses the data operation.
    /// </summary>
    /// <param name="input">The processed input data being reversed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The reversed output data and metadata for the operation.</returns>
    public async Task<DaprOperationPayload<ReadOnlyMemory<byte>>> ReverseAsync(DaprOperationPayload<ReadOnlyMemory<byte>> input, CancellationToken cancellationToken)
    {
        var checksumKey = GetChecksumKey();
        if (input.Metadata.TryGetValue(checksumKey, out var checksum))
        {
            var newChecksum = await CalculateChecksumAsync(input.Payload, cancellationToken);
            if (!string.Equals(checksum, newChecksum))
            {
                throw new DaprException("Data integrity check failed. Checksums do not match.");
            }
        }
        
        //If there's no checksum metadata or it matches, just continue with the next operation
        return new DaprOperationPayload<ReadOnlyMemory<byte>>(input.Payload);
    }

    /// <summary>
    /// Creates the SHA256 representing the checksum on the value.
    /// </summary>
    /// <param name="data">The data to create the hash from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task containing the base64 hash value.</returns>
    private async static Task<string> CalculateChecksumAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        await using var memoryStream = new MemoryStream(data.Length);
        await memoryStream.WriteAsync(data, cancellationToken);
        memoryStream.Position = 0;
        var hash = await sha256.ComputeHashAsync(memoryStream, cancellationToken);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Get the key used to store the hash in the metadata.
    /// </summary>
    /// <returns>The key value.</returns>
    private string GetChecksumKey() => $"{Name}-hash";
}
