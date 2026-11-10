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

using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Dapr.Workflow.Payloads;

internal static class WorkflowPayloadUpload
{
    internal const int BufferSize = 64 * 1024;

    internal static async Task<(long Size, string Hash)> RunAsync(
        Func<Stream, CancellationToken, Task> produce,
        Func<Stream, CancellationToken, Task> upload,
        CancellationToken cancellationToken)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = cancellation.Token;
        var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: BufferSize,
            resumeWriterThreshold: BufferSize / 2, useSynchronizationContext: false));
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var source = new HashingReadStream(pipe.Reader.AsStream(leaveOpen: true), hash);
        using var destination = new BoundedWriteStream(pipe.Writer.AsStream(leaveOpen: true), token);
        ExceptionDispatchInfo? failure = null;

        async Task RunSideAsync(Func<Task> operation, Func<ValueTask> complete)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                await operation().ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                // Preserve the initiating error rather than the other side's consequential cancellation.
                Interlocked.CompareExchange(ref failure, ExceptionDispatchInfo.Capture(ex), null);
                await cancellation.CancelAsync().ConfigureAwait(false);
                throw;
            }
            finally
            {
                await complete().ConfigureAwait(false);
            }
        }

        var uploading = RunSideAsync(async () =>
        {
            await upload(source, token).ConfigureAwait(false);
            if (!source.EndOfStream)
                throw new IOException("The binary store completed the upload without consuming the entire payload.");
        }, () => pipe.Reader.CompleteAsync());

        // A producer may use synchronous Stream writes. Keep it off the calling thread so bounded
        // writes cannot prevent the upload from progressing on that thread's synchronization context.
        var producing = Task.Run(() => RunSideAsync(
            () => produce(destination, token), () =>
            {
                destination.Dispose();
                return pipe.Writer.CompleteAsync();
            }));
        try
        {
            await Task.WhenAll(producing, uploading).ConfigureAwait(false);
        }
        catch
        {
            failure?.Throw();
            throw;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return (source.BytesRead, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }

    private sealed class BoundedWriteStream(Stream inner, CancellationToken operationToken) : Stream
    {
        private bool _disposed;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => !_disposed;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(operationToken, cancellationToken);
            cancellation.Token.ThrowIfCancellationRequested();
            while (!buffer.IsEmpty)
            {
                var count = Math.Min(buffer.Length, BufferSize);
                await inner.WriteAsync(buffer[..count], cancellation.Token).ConfigureAwait(false);
                buffer = buffer[count..];
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Write(byte[] buffer, int offset, int count)
            => WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            operationToken.ThrowIfCancellationRequested();
            var rented = ArrayPool<byte>.Shared.Rent(Math.Min(buffer.Length, BufferSize));
            try
            {
                while (!buffer.IsEmpty)
                {
                    var count = Math.Min(buffer.Length, BufferSize);
                    buffer[..count].CopyTo(rented);
                    Write(rented, 0, count);
                    buffer = buffer[count..];
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented, clearArray: true);
            }
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(operationToken, cancellationToken);
            await inner.FlushAsync(cancellation.Token).ConfigureAwait(false);
        }

        public override void Flush() => FlushAsync(default).GetAwaiter().GetResult();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            _disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class HashingReadStream(Stream inner, IncrementalHash hash) : Stream
    {
        public long BytesRead { get; private set; }
        public bool EndOfStream { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            hash.AppendData(buffer.Span[..read]);
            BytesRead = checked(BytesRead + read);
            if (read == 0 && !buffer.IsEmpty)
                EndOfStream = true;
            return read;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count)
            => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
