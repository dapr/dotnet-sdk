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

using System.Runtime.InteropServices;
using Dapr.Common;

namespace Dapr.BinaryStore;

/// <summary>
/// <para>
/// Defines client operations for interacting with the Dapr binary store building block.
/// </para>
/// <para>
/// Use <c>DaprBinaryStoreClientBuilder</c> to create a standalone
/// <see cref="DaprBinaryStoreClient"/>, or register one for dependency injection via
/// <c>DaprBinaryStoreServiceCollectionExtensions.AddDaprBinaryStoreClient</c>.
/// </para>
/// <para>
/// Implementations of <see cref="DaprBinaryStoreClient"/> implement <see cref="IDisposable"/>
/// because the client accesses network resources. For best performance, create a single long-lived
/// client instance and share it for the lifetime of the application — dependency injection handles
/// this automatically.
/// </para>
/// </summary>
public abstract class DaprBinaryStoreClient : IDaprClient
{
    /// <summary>
    /// The default size, in bytes, of each chunk streamed to the Dapr runtime during an upload.
    /// </summary>
    public const int DefaultChunkSize = 32 * 1024;

    private bool disposed;

    /// <summary>
    /// Stores a binary file in the named binary store, streaming the content to the Dapr runtime
    /// in chunks.
    /// </summary>
    /// <param name="storeName">The name of the Dapr binary store component.</param>
    /// <param name="fileName">
    /// The name of the file. Nested names such as <c>folder/file.bin</c> are permitted; names with a
    /// leading or trailing slash, empty segments, <c>.</c>/<c>..</c> segments or any of
    /// <c>\ * ? [ ] #</c> are rejected by the Dapr runtime.
    /// </param>
    /// <param name="content">The stream to read the file content from, starting at its current position.</param>
    /// <param name="overwrite">
    /// <see langword="true"/> to replace an existing file; <see langword="false"/> (the default) to fail
    /// if the file already exists.
    /// </param>
    /// <param name="chunkSize">The size in bytes of each chunk sent to the runtime.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <exception cref="DaprException">Thrown when the Dapr runtime reports a failure.</exception>
    public abstract Task SetBinaryFileAsync(
        string storeName,
        string fileName,
        Stream content,
        bool overwrite = false,
        int chunkSize = DefaultChunkSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a binary file held in memory in the named binary store.
    /// </summary>
    /// <param name="storeName">The name of the Dapr binary store component.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="content">The file content.</param>
    /// <param name="overwrite">
    /// <see langword="true"/> to replace an existing file; <see langword="false"/> to fail if it exists.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <exception cref="DaprException">Thrown when the Dapr runtime reports a failure.</exception>
    public virtual async Task SetBinaryFileAsync(
        string storeName,
        string fileName,
        ReadOnlyMemory<byte> content,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        var stream = MemoryMarshal.TryGetArray(content, out var segment)
            ? new MemoryStream(segment.Array!, segment.Offset, segment.Count, writable: false)
            : new MemoryStream(content.ToArray(), writable: false);

        await using (stream.ConfigureAwait(false))
        {
            await SetBinaryFileAsync(storeName, fileName, stream, overwrite, DefaultChunkSize, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Retrieves a binary file from the named binary store as a sequence of chunks.
    /// </summary>
    /// <param name="storeName">The name of the Dapr binary store component.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// An asynchronous sequence of the file's chunks, in order. Chunk sizes are chosen by the runtime.
    /// </returns>
    /// <exception cref="DaprException">
    /// Thrown while enumerating when the Dapr runtime reports a failure, for example when the file
    /// does not exist.
    /// </exception>
    public abstract IAsyncEnumerable<ReadOnlyMemory<byte>> GetBinaryFileChunksAsync(
        string storeName,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a binary file and writes its content to <paramref name="destination"/>.
    /// </summary>
    /// <param name="storeName">The name of the Dapr binary store component.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="destination">The stream to write the file content to.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <exception cref="DaprException">Thrown when the Dapr runtime reports a failure.</exception>
    public virtual async Task GetBinaryFileAsync(
        string storeName,
        string fileName,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        await foreach (var chunk in GetBinaryFileChunksAsync(storeName, fileName, cancellationToken)
                           .ConfigureAwait(false))
        {
            await destination.WriteAsync(chunk, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Retrieves a binary file and returns its complete content in memory.
    /// </summary>
    /// <param name="storeName">The name of the Dapr binary store component.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The file content.</returns>
    /// <exception cref="DaprException">Thrown when the Dapr runtime reports a failure.</exception>
    public virtual async Task<byte[]> GetBinaryFileBytesAsync(
        string storeName,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await GetBinaryFileAsync(storeName, fileName, buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    /// <summary>
    /// Deletes a binary file from the named binary store.
    /// </summary>
    /// <param name="storeName">The name of the Dapr binary store component.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <exception cref="DaprException">
    /// Thrown when the Dapr runtime reports a failure, for example when the file does not exist.
    /// </exception>
    public abstract Task DeleteBinaryFileAsync(
        string storeName,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public void Dispose()
    {
        if (!this.disposed)
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
            this.disposed = true;
        }
    }

    /// <summary>
    /// Releases the managed resources used by this instance.
    /// </summary>
    /// <param name="disposing">
    /// <see langword="true"/> if called from <see cref="Dispose()"/>; otherwise
    /// <see langword="false"/>.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
    }
}
