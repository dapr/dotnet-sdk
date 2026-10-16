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

using Dapr.Workflow.Payloads;
using Grpc.Core;

namespace Dapr.Workflow.BinaryStore;

internal sealed class BinaryStorePayloadStream : Stream
{
    private readonly IAsyncEnumerator<ReadOnlyMemory<byte>> _chunks;
    private readonly PayloadReference _reference;
    private ReadOnlyMemory<byte> _currentChunk;
    private int _currentOffset;
    private bool _completed;
    private bool _disposed;

    public BinaryStorePayloadStream(
        IAsyncEnumerator<ReadOnlyMemory<byte>> chunks,
        PayloadReference reference,
        bool hasFirstChunk)
    {
        _chunks = chunks;
        _reference = reference;
        if (hasFirstChunk)
        {
            _currentChunk = chunks.Current;
        }
        else
        {
            _completed = true;
        }
    }

    public override bool CanRead => !_disposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + count, buffer.Length);
        return ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
    }

    public override int Read(Span<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            return 0;
        }

        var temp = new byte[buffer.Length];
        var read = ReadAsync(temp.AsMemory()).AsTask().GetAwaiter().GetResult();
        temp.AsSpan(0, read).CopyTo(buffer);
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (buffer.IsEmpty || _completed)
        {
            return 0;
        }

        while (_currentOffset >= _currentChunk.Length)
        {
            bool hasNext;
            try
            {
                hasNext = await _chunks.MoveNextAsync().ConfigureAwait(false);
            }
            catch (DaprException ex) when (ex.HasStatusCode(StatusCode.NotFound))
            {
                throw new PayloadNotFoundException(_reference, ex);
            }

            if (!hasNext)
            {
                _completed = true;
                return 0;
            }

            _currentChunk = _chunks.Current;
            _currentOffset = 0;
        }

        var count = Math.Min(buffer.Length, _currentChunk.Length - _currentOffset);
        _currentChunk.Slice(_currentOffset, count).CopyTo(buffer);
        _currentOffset += count;
        return count;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + count, buffer.Length);
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _chunks.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _disposed = true;
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await _chunks.DisposeAsync().ConfigureAwait(false);
            _disposed = true;
        }

        await base.DisposeAsync().ConfigureAwait(false);
    }
}
