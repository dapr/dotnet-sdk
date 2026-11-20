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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapr.BinaryStore;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace Dapr.Workflow.Payloads;

/// <summary>
/// An <see cref="IWorkflowPayloadConverter"/> that offloads large workflow payloads to a Dapr
/// binary store and persists a <see cref="PayloadReference"/> envelope in their place.
/// </summary>
/// <remarks>
/// <para>
/// Outbound: the caller supplies a serialized snapshot. If
/// <see cref="BinaryStorePayloadOptions.WritePreference"/> is
/// <see cref="PayloadWritePreference.PreferReference"/>, the boundary is
/// enabled in <see cref="BinaryStorePayloadOptions.OffloadSites"/> and the UTF-8 size of the
/// serialized string meets <see cref="BinaryStorePayloadOptions.OffloadThreshold"/>, the bytes are
/// written to the store under a content-addressed key scoped to the workflow instance and a
/// reference envelope is returned instead. Values that are already a <see cref="PayloadReference"/>
/// are passed through unchanged unless inlining is requested. Inlining resolves the original
/// serialized content with the same integrity and buffering checks as inbound resolution.
/// </para>
/// <para>
/// Inbound: reference envelopes are resolved to their original serialized bytes. The boundary
/// coordinator bypasses decoding when the application explicitly requests a reference.
/// </para>
/// <para>
/// Writes are never retried by this converter because the runtime cannot replay an upload
/// stream. Except for explicit direct streaming writes, idempotency comes from content addressing:
/// a repeated write of identical bytes
/// targets the same key and is skipped when <see cref="BinaryStorePayloadOptions.SkipWriteIfExists"/>
/// is <see langword="true"/>.
/// </para>
/// </remarks>
internal sealed class BinaryStorePayloadConverter : IWorkflowPayloadConverter
{
    private readonly DaprBinaryStoreClient _store;
    private readonly BinaryStorePayloadOptions _options;
    private readonly IWorkflowPayloadManifest _manifest;
    private readonly WorkflowPayloadByteCache? _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryStorePayloadConverter"/> class.
    /// </summary>
    /// <param name="store">The binary store client used for all reads and writes.</param>
    /// <param name="options">The offloading options.</param>
    /// <param name="manifest">The manifest that tracks keys written per instance for later cleanup.</param>
    public BinaryStorePayloadConverter(DaprBinaryStoreClient store,
        IOptions<BinaryStorePayloadOptions> options, IWorkflowPayloadManifest manifest)
        : this(store, options, manifest,
            AppContext.TryGetSwitch(WorkflowPayloadByteCache.SwitchName, out var enabled) && enabled
                ? new WorkflowPayloadByteCache()
                : null)
    {
    }

    internal BinaryStorePayloadConverter(DaprBinaryStoreClient store,
        IOptions<BinaryStorePayloadOptions> options, IWorkflowPayloadManifest manifest,
        WorkflowPayloadByteCache? cache)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _cache = cache;
        _options.Validate();
    }

    /// <inheritdoc />
    public ValueTask<string> EncodeAsync(string serialized, PayloadSite site, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var reference = PayloadReferenceParser.Parse(serialized);
        if (site.RequireInline || _options.WritePreference == PayloadWritePreference.PreferInline)
        {
            return reference is null
                ? new(serialized)
                : new(ResolveStringAsync(reference, cancellationToken));
        }

        if (serialized.Length == 0 || reference is not null ||
            _options.WritePreference == PayloadWritePreference.Preserve)
        {
            return new(serialized);
        }

        return OffloadIfNeededAsync(serialized, site, cancellationToken);
    }

    /// <summary>Stages an automatically offloaded workflow-input stream and returns its reference envelope.</summary>
    /// <param name="content">The caller-owned source stream.</param>
    /// <param name="site">The boundary, owning instance, and available registered names.</param>
    /// <param name="cancellationToken">Cancels staging and upload.</param>
    /// <returns>The serialized reference to the stored raw bytes.</returns>
    internal async Task<string> EncodeStreamAsync(Stream content, PayloadSite site, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (site.RequireInline || _options.WritePreference != PayloadWritePreference.PreferReference ||
            (_options.OffloadSites & site.Kind) == 0)
        {
            throw new InvalidOperationException(
                "Stream workflow inputs require WritePreference = PreferReference and WorkflowInput enabled " +
                "in OffloadSites. Streams are stored as references regardless of OffloadThreshold.");
        }

        var reference = await StoreStreamAsync(content, site.ToPrefixContext(), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return reference.ToEnvelope();
    }

    /// <inheritdoc />
    public async ValueTask<string?> DecodeAsync(string? wire, PayloadSite site,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var reference = PayloadReferenceParser.Parse(wire);
        return reference is null ? wire : await ResolveStringAsync(reference, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="IWorkflowPayloadResolver.OpenAsync"/>
    internal async Task<Stream> OpenAsync(PayloadReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IAsyncEnumerator<ReadOnlyMemory<byte>>? chunks = null;
        var transferred = false;
        try
        {
            chunks = ReadChunksAsync(reference, readCts.Token)
                .GetAsyncEnumerator(readCts.Token);
            var hasFirstChunk = await chunks.MoveNextAsync().ConfigureAwait(false);
            var stream = new BinaryStorePayloadStream(chunks, reference, hasFirstChunk, readCts);
            transferred = true;
            return stream;
        }
        catch (DaprException ex) when (ex.HasStatusCode(StatusCode.NotFound))
        {
            throw new PayloadNotFoundException(reference, ex);
        }
        finally
        {
            if (!transferred)
            {
                try
                {
                    if (chunks is not null)
                        await chunks.DisposeAsync().ConfigureAwait(false);
                }
                finally { readCts.Dispose(); }
            }
        }
    }

    internal async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadChunksAsync(PayloadReference reference,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();
        IAsyncEnumerator<ReadOnlyMemory<byte>> chunks;
        try
        {
            chunks = _store.GetBinaryFileChunksAsync(reference.StoreName, reference.Key, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);
        }
        catch (DaprException ex) when (ex.HasStatusCode(StatusCode.NotFound))
        {
            throw new PayloadNotFoundException(reference, ex);
        }

        await using (chunks.ConfigureAwait(false))
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool hasNext;
                try
                {
                    hasNext = await chunks.MoveNextAsync().ConfigureAwait(false);
                }
                catch (DaprException ex) when (ex.HasStatusCode(StatusCode.NotFound))
                {
                    throw new PayloadNotFoundException(reference, ex);
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (!hasNext)
                    yield break;
                yield return chunks.Current;
            }
        }
    }

    /// <summary>Stores already-serialized content when only the owning instance ID is known.</summary>
    /// <param name="serialized">The serialized payload to encode as UTF-8 bytes.</param>
    /// <param name="instanceId">The owning workflow instance ID.</param>
    /// <param name="cancellationToken">Cancels storage and manifest recording.</param>
    /// <returns>A reference to the stored serialized bytes.</returns>
    internal Task<PayloadReference> StoreSerializedAsync(string serialized, string instanceId,
        CancellationToken cancellationToken = default)
        => StoreSerializedAsync(serialized, new WorkflowPayloadPrefixContext(instanceId), cancellationToken);

    /// <summary>Stores already-serialized content using the supplied prefix metadata.</summary>
    /// <param name="serialized">The serialized payload to encode as UTF-8 bytes.</param>
    /// <param name="context">The available ownership and registered-name metadata.</param>
    /// <param name="cancellationToken">Cancels storage and manifest recording.</param>
    /// <returns>A reference to the stored serialized bytes.</returns>
    internal Task<PayloadReference> StoreSerializedAsync(string serialized, WorkflowPayloadPrefixContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        return StoreSerializedCoreAsync(serialized, Encoding.UTF8.GetByteCount(serialized), context, cancellationToken);
    }

    private async Task<PayloadReference> StoreSerializedCoreAsync(string serialized, int length, WorkflowPayloadPrefixContext context,
        CancellationToken cancellationToken)
    {
        var bytes = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            Encoding.UTF8.GetBytes(serialized.AsSpan(), bytes);
            return await StoreBytesAsync(bytes, length, context, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes, clearArray: true);
        }
    }

    // ---- internals ----

    private async ValueTask<string> OffloadIfNeededAsync(string serialized, PayloadSite site,
        CancellationToken cancellationToken)
    {
        if ((_options.OffloadSites & site.Kind) == 0)
        {
            return serialized;
        }

        var byteCount = Encoding.UTF8.GetByteCount(serialized);
        if (_options.OffloadThreshold is { } threshold && byteCount < threshold)
        {
            return serialized;
        }

        var reference = await StoreSerializedCoreAsync(serialized, byteCount, site.ToPrefixContext(), cancellationToken)
            .ConfigureAwait(false);
        return reference.ToEnvelope();
    }

    /// <summary>Stores a serialized byte buffer when only the owning instance ID is known.</summary>
    /// <param name="bytes">The buffer containing serialized data.</param>
    /// <param name="length">The number of bytes to store from the start of the buffer.</param>
    /// <param name="instanceId">The owning workflow instance ID.</param>
    /// <param name="cancellationToken">Cancels storage and manifest recording.</param>
    /// <returns>A content-addressed reference to the stored bytes.</returns>
    internal Task<PayloadReference> StoreBytesAsync(byte[] bytes, int length, string instanceId,
        CancellationToken cancellationToken)
        => StoreBytesAsync(bytes, length, new WorkflowPayloadPrefixContext(instanceId), cancellationToken);

    /// <summary>Stores a serialized byte buffer using the supplied prefix metadata.</summary>
    /// <param name="bytes">The buffer containing serialized data.</param>
    /// <param name="length">The number of bytes to store from the start of the buffer.</param>
    /// <param name="context">The available ownership and registered-name metadata.</param>
    /// <param name="cancellationToken">Cancels storage and manifest recording.</param>
    /// <returns>A content-addressed reference to the stored bytes.</returns>
    internal async Task<PayloadReference> StoreBytesAsync(byte[] bytes, int length, WorkflowPayloadPrefixContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var instancePrefix = _options.GetInstancePrefix(context);
        var hash = Convert.ToHexString(SHA256.HashData(bytes.AsSpan(0, length))).ToLowerInvariant();
        using var content = new MemoryStream(bytes, 0, length, writable: false);
        return await StoreContentAsync(content, length, hash, context.InstanceId, instancePrefix, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="IWorkflowPayloadResolver.StoreStreamAsync(Stream, string, CancellationToken)"/>
    internal Task<PayloadReference> StoreStreamAsync(Stream content, string instanceId,
        CancellationToken cancellationToken)
        => StoreStreamAsync(content, new WorkflowPayloadPrefixContext(instanceId), cancellationToken);

    /// <inheritdoc cref="IWorkflowPayloadResolver.StoreStreamAsync(Stream, WorkflowPayloadPrefixContext, CancellationToken)"/>
    internal async Task<PayloadReference> StoreStreamAsync(Stream content, WorkflowPayloadPrefixContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(context);
        if (!content.CanRead)
        {
            throw new ArgumentException("The payload stream must be readable.", nameof(content));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var instancePrefix = _options.GetInstancePrefix(context);

        const int bufferSize = 64 * 1024;
        // The content-addressed key must be known before uploading. Stage even seekable sources
        // so the uploaded bytes cannot change between hashing and the second read.
        var fileOptions = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            BufferSize = bufferSize,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose
        };
        if (!OperatingSystem.IsWindows())
        {
            fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }
        await using var staged = new FileStream(
            Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), fileOptions);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = await content.ReadAsync(buffer.AsMemory(0, bufferSize), cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (read == 0)
                    break;

                hash.AppendData(buffer.AsSpan(0, read));
                await staged.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }

        await staged.FlushAsync(cancellationToken).ConfigureAwait(false);
        staged.Position = 0;
        var contentHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        return await StoreContentAsync(staged, staged.Length, contentHash, context.InstanceId, instancePrefix, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc cref="IWorkflowPayloadResolver.WriteAsync(Func{Stream, CancellationToken, Task}, string, CancellationToken)"/>
    internal Task<PayloadReference> WriteAsync(Func<Stream, CancellationToken, Task> write, string instanceId,
        CancellationToken cancellationToken)
        => WriteAsync(write, new WorkflowPayloadPrefixContext(instanceId), cancellationToken);

    /// <inheritdoc cref="IWorkflowPayloadResolver.WriteAsync(Func{Stream, CancellationToken, Task}, WorkflowPayloadPrefixContext, CancellationToken)"/>
    internal async Task<PayloadReference> WriteAsync(Func<Stream, CancellationToken, Task> write, WorkflowPayloadPrefixContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var key = $"{_options.GetInstancePrefix(context)}/uploads/{Guid.NewGuid():N}";
        var collision = false;
        try
        {
            var (size, hash) = await WorkflowPayloadUpload.RunAsync(write,
                async (source, token) =>
                {
                    try
                    {
                        await _store.SetBinaryFileAsync(_options.StoreName, key, source,
                            cancellationToken: token).ConfigureAwait(false);
                    }
                    catch (DaprException ex) when (ex.HasStatusCode(StatusCode.AlreadyExists))
                    {
                        collision = true;
                        throw;
                    }
                }, cancellationToken).ConfigureAwait(false);
            var reference = new PayloadReference(_options.StoreName, key, size, hash);
            await _manifest.RecordAsync(context.InstanceId, reference, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return reference;
        }
        catch (Exception error)
        {
            // A collision is not our object to delete and must never be treated as a successful write.
            if (collision)
                throw;

            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await _store.DeleteBinaryFileAsync(_options.StoreName, key, cleanup.Token).ConfigureAwait(false);
            }
            catch (DaprException ex) when (ex.HasStatusCode(StatusCode.NotFound))
            {
                // The failed upload did not leave an object, or the runtime already removed it.
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("Payload upload failed and its object could not be cleaned up.",
                    error, cleanupError);
            }
            throw;
        }
    }

    private async Task<PayloadReference> StoreContentAsync(Stream content, long length, string hash,
        string instanceId, string instancePrefix, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = $"{instancePrefix}/{hash}";
        var reference = new PayloadReference(_options.StoreName, key, length, hash);

        try
        {
            // A replica-local read cache cannot establish whether the shared store still contains this key.
            await _store.SetBinaryFileAsync(_options.StoreName, key, content, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DaprException ex) when (ex.HasStatusCode(StatusCode.AlreadyExists) && _options.SkipWriteIfExists)
        {
            // Same instance, same content hash: identical bytes are already present.
        }

        await _manifest.RecordAsync(instanceId, reference, cancellationToken).ConfigureAwait(false);
        return reference;
    }

    internal async Task<string> ResolveStringAsync(PayloadReference reference, CancellationToken cancellationToken)
        => Encoding.UTF8.GetString((await ResolveBytesAsync(reference, cancellationToken).ConfigureAwait(false)).Span);

    internal void Invalidate(PayloadReference reference) => _cache?.Invalidate(reference);

    internal async Task<ReadOnlyMemory<byte>> ResolveBytesAsync(PayloadReference reference,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();
        if (reference.Size > _options.MaxInMemoryRehydrationSize)
        {
            throw new PayloadRehydrationLimitException(reference, _options.MaxInMemoryRehydrationSize);
        }

        if (reference.Stages.Count > 0)
        {
            throw new NotSupportedException(
                $"Payload '{reference.Key}' was written with stages [{string.Join(", ", reference.Stages.Select(s => s.Name))}] " +
                "but this converter does not support stage reversal.");
        }

        var generation = _cache?.Generation;
        if (_options.VerifyIntegrityOnRead && _cache?.TryGet(reference, out var cached) == true &&
            cached.Length <= _options.MaxInMemoryRehydrationSize)
        {
            return cached;
        }

        return await ReadBytesAsync(reference, generation, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ReadOnlyMemory<byte>> ReadBytesAsync(PayloadReference reference, long? generation,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream(reference.Size is > 0 and <= int.MaxValue ? (int)reference.Size : 0);
        try
        {
            await foreach (var chunk in _store.GetBinaryFileChunksAsync(reference.StoreName, reference.Key, cancellationToken)
                               .WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (buffer.Length + chunk.Length > _options.MaxInMemoryRehydrationSize)
                {
                    throw new PayloadRehydrationLimitException(reference, _options.MaxInMemoryRehydrationSize);
                }

                buffer.Write(chunk.Span);
            }
        }
        catch (DaprException ex) when (ex.HasStatusCode(StatusCode.NotFound))
        {
            throw new PayloadNotFoundException(reference, ex);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var bytes = buffer.GetBuffer().AsMemory(0, checked((int)buffer.Length));

        if (_options.VerifyIntegrityOnRead)
        {
            var actual = Convert.ToHexString(SHA256.HashData(bytes.Span)).ToLowerInvariant();
            if (!string.Equals(actual, reference.ContentHash, StringComparison.Ordinal))
            {
                throw new PayloadIntegrityException(reference, actual);
            }

            if (_cache is not null && generation is { } observed)
            {
                _cache.Add(reference, bytes, buffer.Capacity, observed);
            }
        }

        return bytes;
    }

}
