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
/// orchestrators through <see cref="WorkflowContextPayloadExtensions"/>, by activities through
/// <see cref="WorkflowActivityContextPayloadExtensions"/>, and by clients through dependency injection.
/// </summary>
/// <remarks>
/// Orchestrator code must treat this interface as read-only. Resolution of an existing reference is
/// replay-safe because stored payloads are immutable; creating new payloads
/// from an orchestrator is not, and the <see cref="WorkflowContext"/> extension methods deliberately
/// do not expose storage or upload methods.
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
    /// Retrieves and deserializes a typed reference, inferring the result type from the reference.
    /// This materializes the complete value; use raw streaming reads for large payloads.
    /// </summary>
    /// <typeparam name="T">The type described by the reference.</typeparam>
    /// <param name="reference">The typed reference to resolve.</param>
    /// <param name="cancellationToken">A token used to cancel resolution.</param>
    /// <returns>The deserialized value.</returns>
    Task<T> ResolveAsync<T>(PayloadReference<T> reference, CancellationToken cancellationToken = default)
        => ResolveAsync<T>((PayloadReference)reference, cancellationToken);
 
    /// <summary>
    /// Opens a read-only stream over the raw stored bytes identified by <paramref name="reference"/>
    /// without buffering or deserializing them. The caller owns the returned stream.
    /// </summary>
    /// <param name="reference">The reference to open.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A stream positioned at the start of the payload.</returns>
    /// <remarks>
    /// This is a raw-byte read: payload stages are not reversed and integrity is not verified.
    /// Dispose the stream when finished; early disposal does not consume the remaining bytes.
    /// </remarks>
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

    /// <summary>
    /// Stages the remaining raw bytes of <paramref name="content"/> to temporary disk, then uploads
    /// them under a content-addressed key. Use <see cref="UploadAsync(Stream, string, CancellationToken)"/>
    /// to upload immediately without a staging file.
    /// Intended for activities and clients, not orchestrator code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reads from the current position through end of stream, including for non-seekable streams.
    /// The caller retains ownership: the stream is not disposed or rewound, including on failure.
    /// Do not read, seek, or modify the stream concurrently with this operation.
    /// </para>
    /// <para>
    /// The built-in resolver stages bytes in a temporary file while computing SHA-256, then uploads
    /// that file under a content-addressed key. Memory usage is bounded, but temporary disk space
    /// proportional to the payload is required. The temporary file is deleted when the operation
    /// completes, fails, or is canceled. No reference is returned until upload and manifest recording
    /// succeed. This explicit write is independent of automatic offloading policy and rehydration limits.
    /// </para>
    /// <para>
    /// Consume raw bytes using <see cref="OpenAsync"/> or <see cref="ReadChunksAsync(PayloadReference, CancellationToken)"/>.
    /// Only use <see cref="PayloadReference.AsTyped{T}"/> or materialized reads when the bytes match
    /// the configured serializer's format for that type. Preserve references at sending boundaries;
    /// inlining arbitrary binary data is not supported.
    /// Existing custom resolvers default to throwing <see cref="NotSupportedException"/>.
    /// </para>
    /// </remarks>
    /// <param name="content">A readable stream containing the bytes to store.</param>
    /// <param name="instanceId">The workflow instance that owns the payload.</param>
    /// <param name="cancellationToken">A token used to cancel reading, staging, uploading, and manifest recording.</param>
    /// <returns>An untyped reference containing the stored byte count and SHA-256 hash.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    /// <exception cref="ArgumentException">The stream is not readable or the instance ID is blank.</exception>
    /// <exception cref="NotSupportedException">The resolver does not support storing streams.</exception>
    Task<PayloadReference> StoreStreamAsync(Stream content, string instanceId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This workflow payload resolver does not support storing streams.");

    /// <summary>
    /// Uploads remaining source bytes immediately with bounded buffering and no temporary file,
    /// returning an untyped reference with the final SHA-256 and length.
    /// </summary>
    /// <remarks>
    /// Reads from the current position through EOF, including non-seekable streams. The caller owns
    /// the source; it is not rewound or disposed on success or failure. Do not access it concurrently.
    /// Uses a unique key, not content-addressed deduplication. Reopen or rewind before retrying.
    /// </remarks>
    /// <param name="content">A readable raw-byte source; no serialization is performed.</param>
    /// <param name="instanceId">The workflow instance that owns the payload.</param>
    /// <param name="cancellationToken">Cancels reading and upload.</param>
    /// <returns>The completed upload's reference.</returns>
    Task<PayloadReference> UploadAsync(Stream content, string instanceId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        if (!content.CanRead)
            throw new ArgumentException("The payload stream must be readable.", nameof(content));
        cancellationToken.ThrowIfCancellationRequested();
        return UploadAsync((destination, token) => content.CopyToAsync(destination, token), instanceId, cancellationToken);
    }

    /// <summary>
    /// Uploads asynchronous raw-byte chunks as they arrive, with backpressure and no temporary file.
    /// Chunk boundaries are not preserved as records in storage.
    /// </summary>
    /// <remarks>
    /// Enumerates once, consuming each chunk before advancing so producers may reuse their buffers.
    /// Enumeration is disposed on completion, failure, or cancellation. Empty chunks are permitted.
    /// This does not serialize objects or frame JSON records; supply already encoded bytes.
    /// Uses the same unique-key, integrity metadata, and failure semantics as the producer overload.
    /// </remarks>
    /// <param name="chunks">The raw-byte sequence to upload.</param>
    /// <param name="instanceId">The workflow instance that owns the payload.</param>
    /// <param name="cancellationToken">Cancels enumeration and upload.</param>
    /// <returns>The completed upload's reference.</returns>
    Task<PayloadReference> UploadAsync(IAsyncEnumerable<ReadOnlyMemory<byte>> chunks, string instanceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        cancellationToken.ThrowIfCancellationRequested();
        return UploadAsync(async (destination, token) =>
        {
            await foreach (var chunk in chunks.WithCancellation(token).ConfigureAwait(false))
                await destination.WriteAsync(chunk, token).ConfigureAwait(false);
        }, instanceId, cancellationToken);
    }

    /// <summary>
    /// Produces raw bytes directly into a binary-store upload with bounded buffering and no temporary file.
    /// The SDK owns the destination; finish and flush wrappers with leave-open enabled before returning.
    /// </summary>
    /// <remarks>
    /// Uses a unique immutable key and incremental SHA-256/length metadata. The reference is returned
    /// only after production, upload, and manifest recording succeed. Failures cancel both sides and
    /// attempt cleanup; retries create distinct keys. See <see cref="WriteAsync"/> for the full lifetime
    /// and failure contract. Existing resolvers implementing <see cref="WriteAsync"/> support this overload.
    /// </remarks>
    /// <param name="write">The sequential producer, invoked at most once with an SDK-owned destination.</param>
    /// <param name="instanceId">The workflow instance that owns the payload.</param>
    /// <param name="cancellationToken">Cancels production, upload, and manifest recording.</param>
    /// <returns>The completed upload's reference.</returns>
    Task<PayloadReference> UploadAsync(Func<Stream, CancellationToken, Task> write, string instanceId,
        CancellationToken cancellationToken = default)
        => WriteAsync(write, instanceId, cancellationToken);

    /// <summary>
    /// Produces raw bytes directly into a binary-store upload and returns their reference.
    /// Compatibility entry point; prefer <see cref="UploadAsync(Func{Stream, CancellationToken, Task}, string, CancellationToken)"/>.
    /// Intended for activities and clients, not orchestrator code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The built-in resolver uploads under a unique immutable key, computing SHA-256 and length
    /// incrementally. A bounded buffer applies backpressure; no temporary file or whole-payload
    /// buffer is used. Unlike <see cref="StoreStreamAsync"/>, identical content and activity retries
    /// create distinct keys. Automatic offloading policy and rehydration limits do not restrict writes.
    /// </para>
    /// <para>
    /// The SDK owns the non-seekable, write-only destination. Write sequentially, honor the supplied
    /// cancellation token, and finish and flush any wrappers with leave-open enabled before returning.
    /// Do not dispose or retain the destination. For a readable source, use
    /// <c>(destination, token) =&gt; source.CopyToAsync(destination, token)</c>; the caller owns the source.
    /// </para>
    /// <para>
    /// A reference is returned only after the producer, upload, and manifest recording succeed.
    /// Failures cancel both producer and upload, then attempt deletion of the unique key.
    /// Cleanup failures are reported alongside the original error. Process termination or ambiguous
    /// remote failures can still leave orphaned objects; this is not a storage transaction.
    /// Consume the reference through raw streaming reads unless the bytes match the configured serializer.
    /// Preserve references at sending boundaries to avoid inlining raw data.
    /// </para>
    /// </remarks>
    /// <param name="write">The producer, invoked at most once with the destination and an operation cancellation token.</param>
    /// <param name="instanceId">The workflow instance that owns the payload.</param>
    /// <param name="cancellationToken">Cancels production, upload, and manifest recording.</param>
    /// <returns>A reference containing the final byte count and SHA-256 hash.</returns>
    /// <exception cref="NotSupportedException">The resolver does not support direct streaming writes.</exception>
    Task<PayloadReference> WriteAsync(Func<Stream, CancellationToken, Task> write, string instanceId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This workflow payload resolver does not support direct streaming writes.");
}
