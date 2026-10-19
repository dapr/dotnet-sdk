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
using System.Linq;
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
/// stream. Idempotency comes from content addressing: a repeated write of identical bytes
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

    /// <inheritdoc />
    public async ValueTask<string?> DecodeAsync(string? wire, PayloadSite site,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var reference = PayloadReferenceParser.Parse(wire);
        return reference is null ? wire : await ResolveStringAsync(reference, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<Stream> OpenAsync(PayloadReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var chunks = _store.GetBinaryFileChunksAsync(reference.StoreName, reference.Key, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        try
        {
            var hasFirstChunk = await chunks.MoveNextAsync().ConfigureAwait(false);
            return new BinaryStorePayloadStream(chunks, reference, hasFirstChunk);
        }
        catch (DaprException ex) when (ex.HasStatusCode(StatusCode.NotFound))
        {
            await chunks.DisposeAsync().ConfigureAwait(false);
            throw new PayloadNotFoundException(reference, ex);
        }
        catch
        {
            await chunks.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal Task<PayloadReference> StoreSerializedAsync(string serialized, string instanceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        cancellationToken.ThrowIfCancellationRequested();
        return StoreSerializedCoreAsync(serialized, Encoding.UTF8.GetByteCount(serialized), instanceId, cancellationToken);
    }

    private async Task<PayloadReference> StoreSerializedCoreAsync(string serialized, int length, string instanceId,
        CancellationToken cancellationToken)
    {
        var bytes = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            Encoding.UTF8.GetBytes(serialized.AsSpan(), bytes);
            return await StoreBytesAsync(bytes, length, instanceId, cancellationToken).ConfigureAwait(false);
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

        var reference = await StoreSerializedCoreAsync(serialized, byteCount, site.InstanceId, cancellationToken)
            .ConfigureAwait(false);
        return reference.ToEnvelope();
    }

    internal async Task<PayloadReference> StoreBytesAsync(byte[] bytes, int length, string instanceId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hash = Convert.ToHexString(SHA256.HashData(bytes.AsSpan(0, length))).ToLowerInvariant();
        var key = $"{_options.KeyPrefix}/{instanceId}/{hash}";
        var reference = new PayloadReference(_options.StoreName, key, length, hash);

        try
        {
            // A replica-local read cache cannot establish whether the shared store still contains this key.
            using var content = new MemoryStream(bytes, 0, length, writable: false);
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
