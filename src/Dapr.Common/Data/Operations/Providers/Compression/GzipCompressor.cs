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

using System.IO.Compression;

namespace Dapr.Common.Data.Operations.Providers.Compression;

/// <inheritdoc />
public sealed class GzipCompressor : IDaprDataCompressor
{
    /// <summary>
    /// The name of the operation.
    /// </summary>
    public string Name => "Dapr.Compression.Gzip";

    /// <summary>
    /// Executes the data processing operation. 
    /// </summary>
    /// <param name="input">The input data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The output data and metadata for the operation.</returns>
    public async Task<DaprOperationPayload<ReadOnlyMemory<byte>>> ExecuteAsync(ReadOnlyMemory<byte> input, CancellationToken cancellationToken = default)
    {
        using var outputStream = new MemoryStream();
        await using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
        {
            await gzipStream.WriteAsync(input, cancellationToken);
        }

        //Replace the existing payload with the compressed payload
        return new DaprOperationPayload<ReadOnlyMemory<byte>>(outputStream.ToArray());
    }

    /// <summary>
    /// Reverses the data operation.
    /// </summary>
    /// <param name="input">The processed input data being reversed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The reversed output data and metadata for the operation.</returns>
    public async Task<DaprOperationPayload<ReadOnlyMemory<byte>>> ReverseAsync(DaprOperationPayload<ReadOnlyMemory<byte>> input, CancellationToken cancellationToken)
    {
        using var inputStream = new MemoryStream(input.Payload.ToArray());
        await using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
        using var outputStream = new MemoryStream();
        await gzipStream.CopyToAsync(outputStream, cancellationToken);
        return new DaprOperationPayload<ReadOnlyMemory<byte>>(outputStream.ToArray());
    }
}
