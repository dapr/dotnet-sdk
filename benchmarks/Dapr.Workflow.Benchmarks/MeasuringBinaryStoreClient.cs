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

using System.Runtime.CompilerServices;
using Dapr.BinaryStore;

namespace Dapr.Workflow.Benchmarks;

/// <summary>
/// Counts file-content bytes consumed by the SDK upload path and returned by its download path.
/// Upload attempts rejected as duplicates are included; these are not network packet measurements.
/// </summary>
internal sealed class MeasuringBinaryStoreClient(DaprBinaryStoreClient inner, MeasurementWindow window)
    : DaprBinaryStoreClient
{
    public override async Task SetBinaryFileAsync(string storeName, string fileName, Stream content,
        bool overwrite = false, int chunkSize = DefaultChunkSize, CancellationToken cancellationToken = default)
    {
        var measurement = window.Active;
        measurement?.BinaryWriteStarted();
        using var counted = new UploadStream(content, count => measurement?.Uploaded(count));
        await inner.SetBinaryFileAsync(storeName, fileName, counted, overwrite, chunkSize, cancellationToken);
    }

    public override async IAsyncEnumerable<ReadOnlyMemory<byte>> GetBinaryFileChunksAsync(
        string storeName, string fileName, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var measurement = window.Active;
        measurement?.BinaryReadStarted();
        await foreach (var chunk in inner.GetBinaryFileChunksAsync(storeName, fileName, cancellationToken))
        {
            measurement?.Downloaded(chunk.Length);
            yield return chunk;
        }
    }

    public override Task DeleteBinaryFileAsync(string storeName, string fileName, CancellationToken cancellationToken = default)
        => inner.DeleteBinaryFileAsync(storeName, fileName, cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (disposing) inner.Dispose();
        base.Dispose(disposing);
    }

    private sealed class UploadStream(Stream inner, Action<int> count) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int length) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int length)
        {
            var read = inner.Read(buffer, offset, length);
            count(read);
            return read;
        }
        public override int Read(Span<byte> buffer)
        {
            var read = inner.Read(buffer);
            count(read);
            return read;
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(buffer, cancellationToken);
            count(read);
            return read;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, length), cancellationToken).AsTask();
        // The caller owns the original stream.
    }
}
