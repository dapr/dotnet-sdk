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
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Dapr.Workflow.Payloads;

/// <summary>
/// Resolves and creates <see cref="PayloadReference"/> instances against a binary store. Accessed by
/// orchestrators through <see cref="WorkflowContextPayloadExtensions"/> and by activities and clients
/// through dependency injection.
/// </summary>
/// <remarks>
/// Orchestrator code must treat this interface as read-only. Resolution of an existing reference is
/// replay-safe because stored payloads are immutable and content-addressed; creating new payloads
/// from an orchestrator is not, and the <see cref="WorkflowContext"/> extension methods deliberately
/// do not expose <see cref="StoreAsync{T}"/>.
/// </remarks>
public interface IWorkflowPayloadResolver
{
    /// <summary>
    /// Retrieves and deserializes the payload identified by <paramref name="reference"/>.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the payload to.</typeparam>
    /// <param name="reference">The reference to resolve.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The deserialized payload.</returns>
    /// <exception cref="PayloadNotFoundException">The stored payload no longer exists.</exception>
    /// <exception cref="PayloadIntegrityException">The retrieved bytes did not match the recorded hash.</exception>
    Task<T> ResolveAsync<T>(PayloadReference reference, CancellationToken cancellationToken = default);
 
    /// <summary>
    /// Opens a read-only stream over the raw stored bytes identified by <paramref name="reference"/>
    /// without buffering or deserializing them. The caller owns the returned stream.
    /// </summary>
    /// <param name="reference">The reference to open.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A stream positioned at the start of the payload.</returns>
    /// <exception cref="PayloadNotFoundException">The stored payload no longer exists.</exception>
    Task<Stream> OpenAsync(PayloadReference reference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enumerates the raw stored bytes identified by <paramref name="reference"/> without buffering
    /// the entire payload or deserializing it. Storage is opened when enumeration begins and released
    /// when enumeration completes or the enumerator is disposed.
    /// </summary>
    /// <remarks>
    /// Consume or copy each chunk before advancing the enumerator; its memory may be reused.
    /// Chunk sizes and boundaries are unspecified. Like <see cref="OpenAsync"/>, this method does not
    /// reverse payload stages or verify integrity. Each enumeration opens a new read.
    /// The default implementation reads from <see cref="OpenAsync"/> using a bounded buffer;
    /// implementations may provide native chunks instead.
    /// </remarks>
    /// <param name="reference">The reference to read.</param>
    /// <param name="cancellationToken">A token used to cancel opening and reading the payload.</param>
    /// <returns>An asynchronous sequence of payload chunks.</returns>
    /// <exception cref="PayloadNotFoundException">The stored payload no longer exists.</exception>
    async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadChunksAsync(PayloadReference reference,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();
        // Preserve the execution context when OpenAsync supplies a queued orchestrator stream.
        await using var stream = await OpenAsync(reference, cancellationToken);
        var buffer = new byte[64 * 1024];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (read == 0)
                yield break;
            yield return buffer.AsMemory(0, read);
        }
    }

    /// <summary>
    /// Enumerates raw stored bytes in chunks of <paramref name="chunkSize"/> bytes, except
    /// for the final chunk, which may be smaller. Storage is opened when enumeration begins
    /// and released when enumeration completes or the enumerator is disposed.
    /// </summary>
    /// <remarks>
    /// The default implementation fills a reusable buffer through <see cref="OpenAsync"/>.
    /// Consume or copy each chunk before advancing the enumerator. Each enumeration opens a new read.
    /// This controls application-visible chunks, not transport message sizes. Payload stages are not
    /// reversed and integrity is not verified. Argument validation occurs when enumeration begins.
    /// </remarks>
    /// <param name="reference">The reference to read.</param>
    /// <param name="chunkSize">The positive number of bytes per full chunk.</param>
    /// <param name="cancellationToken">A token used to cancel opening and reading the payload.</param>
    /// <returns>An asynchronous sequence of payload chunks. Empty payloads produce no chunks.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="chunkSize"/> is not positive.</exception>
    /// <exception cref="PayloadNotFoundException">The stored payload no longer exists.</exception>
    async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadChunksAsync(PayloadReference reference, int chunkSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);
        cancellationToken.ThrowIfCancellationRequested();
        // Keep every read on the captured context, including reads that fill a partial chunk.
        await using var stream = await OpenAsync(reference, cancellationToken);
        var buffer = new byte[chunkSize];
        while (true)
        {
            var filled = 0;
            while (filled < buffer.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = await stream.ReadAsync(buffer.AsMemory(filled), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (read == 0)
                    break;
                filled += read;
            }

            if (filled == 0)
                yield break;
            yield return buffer.AsMemory(0, filled);
            if (filled < buffer.Length)
                yield break;
        }
    }
 
    /// <summary>
    /// Serializes and stores <paramref name="value"/> under a key scoped to <paramref name="instanceId"/>
    /// and returns a reference to it. Intended for activities and clients that want to produce a
    /// reference explicitly rather than rely on automatic offloading.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to store.</param>
    /// <param name="instanceId">The workflow instance that owns the payload.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A typed reference to the stored payload.</returns>
    Task<PayloadReference<T>> StoreAsync<T>(T value, string instanceId, CancellationToken cancellationToken = default);
}
