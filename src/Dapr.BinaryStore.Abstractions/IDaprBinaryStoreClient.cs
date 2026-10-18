// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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

namespace Dapr.BinaryStore;

/// <summary>
/// Defines the operations of a typed client that is bound to a single Dapr binary store component.
/// </summary>
/// <remarks>
/// Extend this interface with a <c>partial interface</c> annotated with <see cref="BinaryStoreAttribute"/>
/// and the source generator will emit the implementation that forwards each call to a
/// <see cref="DaprBinaryStoreClient"/> with the store name pre-filled.
/// </remarks>
public interface IDaprBinaryStoreClient
{
    /// <summary>
    /// Stores a binary file, streaming the content to the Dapr runtime in chunks.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="content">The stream to read the file content from.</param>
    /// <param name="overwrite">Whether to replace an existing file.</param>
    /// <param name="chunkSize">The size in bytes of each chunk sent to the runtime.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task SetBinaryFileAsync(
        string fileName,
        Stream content,
        bool overwrite = false,
        int chunkSize = DaprBinaryStoreClient.DefaultChunkSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a binary file held in memory.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="content">The file content.</param>
    /// <param name="overwrite">Whether to replace an existing file.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task SetBinaryFileAsync(
        string fileName,
        ReadOnlyMemory<byte> content,
        bool overwrite = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a binary file as a sequence of chunks.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    IAsyncEnumerable<ReadOnlyMemory<byte>> GetBinaryFileChunksAsync(
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a binary file and writes its content to <paramref name="destination"/>.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="destination">The stream to write the file content to.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task GetBinaryFileAsync(
        string fileName,
        Stream destination,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a binary file and returns its complete content in memory.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task<byte[]> GetBinaryFileBytesAsync(
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a binary file.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task DeleteBinaryFileAsync(
        string fileName,
        CancellationToken cancellationToken = default);
}
