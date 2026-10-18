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
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Dapr.Workflow.Payloads;

internal sealed class QueuedWorkflowPayloadResolver(
    IWorkflowPayloadResolver resolver,
    WorkflowPayloadHandler payloads) : IWorkflowPayloadResolver
{
    public Task<T> ResolveAsync<T>(PayloadReference reference, CancellationToken cancellationToken = default)
        => payloads.QueueAsync(() => resolver.ResolveAsync<T>(reference,
            cancellationToken.CanBeCanceled ? cancellationToken : payloads.StoppingToken));

    public Task<Stream> OpenAsync(PayloadReference reference, CancellationToken cancellationToken = default)
        => payloads.QueueAsync<Stream>(async () =>
            new QueuedReadStream(await resolver.OpenAsync(reference,
                    cancellationToken.CanBeCanceled ? cancellationToken : payloads.StoppingToken).ConfigureAwait(false),
                payloads));

    public Task<PayloadReference<T>> StoreAsync<T>(T value, string instanceId,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Store payloads in activities or clients, not in orchestrator code.");

    private sealed class QueuedReadStream(Stream inner, WorkflowPayloadHandler payloads) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => new(payloads.QueueAsync(() => inner.ReadAsync(buffer,
                cancellationToken.CanBeCanceled ? cancellationToken : payloads.StoppingToken).AsTask()));

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotSupportedException("Use asynchronous reads for payload streams in orchestrator code.");

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                throw new NotSupportedException("Use asynchronous disposal for payload streams in orchestrator code.");
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await payloads.QueueAsync(async () =>
            {
                await inner.DisposeAsync().ConfigureAwait(false);
                return true;
            }).ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }
}
