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

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Dapr.Workflow.BinaryStore;
using Dapr.Workflow.Payloads;
using Dapr.Workflow.Serialization;
using Microsoft.Extensions.Options;

namespace System.Runtime.CompilerServices;


/// <summary>
/// An <see cref="IWorkflowPayloadConverter"/> that offloads large workflow payloads to a Dapr
/// binary store and persists a <see cref="PayloadReference"/> envelope in their place.
/// </summary>
/// <remarks>
/// <para>
/// Outbound: the value is serialized with <see cref="IWorkflowSerializer"/>. If the boundary is
/// enabled in <see cref="BinaryStorePayloadOptions.OffloadSites"/> and the UTF-8 size of the
/// serialized string meets <see cref="BinaryStorePayloadOptions.OffloadThreshold"/>, the bytes are
/// written to the store under a content-addressed key scoped to the workflow instance and a
/// reference envelope is returned instead. Values that are already a <see cref="PayloadReference"/>
/// are passed through unchanged and never re-offloaded.
/// </para>
/// <para>
/// Inbound: if the string is a reference envelope and the target type is not itself a reference
/// type, the payload is retrieved, optionally verified, decoded as UTF-8 and deserialized. If the
/// target type is <see cref="PayloadReference"/> or <see cref="PayloadReference{T}"/>, the envelope
/// is parsed and returned without retrieval so the consumer can stream it.
/// </para>
/// <para>
/// Writes are never retried by this converter because the runtime cannot replay an upload
/// stream. Idempotency comes from content addressing: a repeated write of identical bytes
/// targets the same key and is skipped when <see cref="BinaryStorePayloadOptions.SkipWriteIfExists"/>
/// is <see langword="true"/>.
/// </para>
/// </remarks>
public sealed class BinaryStorePayloadConverter : IWorkflowPayloadConverter, IWorkflowPayloadResolver
{
    private readonly DaprBinaryStoreClient _store;
    private readonly IWorkflowSerializer _serializer;
    private readonly BinaryStorePayloadOptions _options;
    private readonly IWorkflowPayloadManifest _manifest;
 
    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryStorePayloadConverter"/> class.
    /// </summary>
    /// <param name="store">The binary store client used for all reads and writes.</param>
    /// <param name="serializer">The workflow serializer applied to payload values.</param>
    /// <param name="options">The offloading options.</param>
    /// <param name="manifest">The manifest that tracks keys written per instance for later cleanup.</param>
    public BinaryStorePayloadConverter(DaprBinaryStoreClient store, IWorkflowSerializer serializer,
        IOptions<BinaryStorePayloadOptions> options, IWorkflowPayloadManifest manifest)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        _options.Validate();
    }
 
    // ---- IWorkflowPayloadConverter ----
 
    /// <inheritdoc />
    public ValueTask<string> ToWireAsync<T>(T value, PayloadSite site, CancellationToken cancellationToken = default)
    {
        if (value is PayloadReference existing)
        {
            return new ValueTask<string>(existing.ToEnvelope());
        }
 
        return OffloadIfNeededAsync(_serializer.Serialize(value), site, cancellationToken);
    }
 
    /// <inheritdoc />
    [RequiresUnreferencedCode("Serialization with a runtime Type may require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("Serialization with a runtime Type requires dynamic code generation.")]
    public ValueTask<string> ToWireAsync(object? value, Type? inputType, PayloadSite site, CancellationToken cancellationToken = default)
    {
        if (value is PayloadReference existing)
        {
            return new ValueTask<string>(existing.ToEnvelope());
        }
 
        return OffloadIfNeededAsync(_serializer.Serialize(value, inputType), site, cancellationToken);
    }
 
    /// <inheritdoc />
    public async ValueTask<T?> FromWireAsync<T>(string? wire, PayloadSite site, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(wire))
        {
            return default;
        }
 
        if (!PayloadReference.TryParse(wire, out var reference) || reference is null)
        {
            return _serializer.Deserialize<T>(wire);
        }
 
        if (typeof(T) == typeof(PayloadReference))
        {
            return (T)(object)reference;
        }
 
        if (PayloadReference.IsReferenceType(typeof(T)))
        {
            return (T)(object)CoerceReference(reference, typeof(T));
        }
 
        var json = await ResolveStringAsync(reference, cancellationToken).ConfigureAwait(false);
        return _serializer.Deserialize<T>(json);
    }
 
    /// <inheritdoc />
    [RequiresUnreferencedCode("Deserialization with a runtime Type may require types that cannot be statically analyzed.")]
    [RequiresDynamicCode("Deserialization with a runtime Type requires dynamic code generation.")]
    public async ValueTask<object?> FromWireAsync(string? wire, Type returnType, PayloadSite site, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(returnType);
 
        if (string.IsNullOrEmpty(wire))
        {
            return null;
        }
 
        if (!PayloadReference.TryParse(wire, out var reference) || reference is null)
        {
            return _serializer.Deserialize(wire, returnType);
        }
 
        if (PayloadReference.IsReferenceType(returnType))
        {
            return CoerceReference(reference, returnType);
        }
 
        var json = await ResolveStringAsync(reference, cancellationToken).ConfigureAwait(false);
        return _serializer.Deserialize(json, returnType);
    }
 
    // ---- IWorkflowPayloadResolver ----
 
    /// <inheritdoc />
    public async Task<T> ResolveAsync<T>(PayloadReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var json = await ResolveStringAsync(reference, cancellationToken).ConfigureAwait(false);
        return _serializer.Deserialize<T>(json)
               ?? throw new InvalidOperationException($"Payload '{reference.Key}' deserialized to null for type {typeof(T)}.");
    }
 
    /// <inheritdoc />
    public async Task<Stream> OpenAsync(PayloadReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        try
        {
            return await _store.GetAsync(reference.StoreName, reference.Key, cancellationToken).ConfigureAwait(false);
        }
        catch (DaprBinaryStoreException ex) when (ex.ErrorCode == DaprBinaryStoreErrorCode.NotFound)
        {
            throw new PayloadNotFoundException(reference, ex);
        }
    }
 
    /// <inheritdoc />
    public async Task<PayloadReference<T>> StoreAsync<T>(T value, string instanceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        var bytes = Encoding.UTF8.GetBytes(_serializer.Serialize(value));
        var reference = await StoreBytesAsync(bytes, instanceId, cancellationToken).ConfigureAwait(false);
        return reference.AsTyped<T>();
    }
 
    // ---- internals ----
 
    private async ValueTask<string> OffloadIfNeededAsync(string serialized, PayloadSite site, CancellationToken cancellationToken)
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
 
        var bytes = Encoding.UTF8.GetBytes(serialized);
        var reference = await StoreBytesAsync(bytes, site.InstanceId, cancellationToken).ConfigureAwait(false);
        return reference.ToEnvelope();
    }
 
    private async Task<PayloadReference> StoreBytesAsync(byte[] bytes, string instanceId, CancellationToken cancellationToken)
    {
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var key = $"{_options.KeyPrefix}/{instanceId}/{hash}";
 
        try
        {
            using var content = new MemoryStream(bytes, writable: false);
            await _store.CreateAsync(_options.StoreName, key, content, cancellationToken).ConfigureAwait(false);
        }
        catch (DaprBinaryStoreException ex) when (ex.ErrorCode == DaprBinaryStoreErrorCode.AlreadyExists && _options.SkipWriteIfExists)
        {
            // Same instance, same content hash: identical bytes are already present.
        }
 
        var reference = new PayloadReference(_options.StoreName, key, bytes.LongLength, hash);
        await _manifest.RecordAsync(instanceId, reference, cancellationToken).ConfigureAwait(false);
        return reference;
    }
 
    private async Task<string> ResolveStringAsync(PayloadReference reference, CancellationToken cancellationToken)
    {
        if (reference.Size > _options.MaxInMemoryRehydrationSize)
        {
            throw new InvalidOperationException(
                $"Payload '{reference.Key}' is {reference.Size} bytes, which exceeds MaxInMemoryRehydrationSize " +
                $"({_options.MaxInMemoryRehydrationSize}). Declare the parameter as PayloadReference<T> and stream it instead.");
        }
 
        if (reference.Stages.Count > 0)
        {
            throw new NotSupportedException(
                $"Payload '{reference.Key}' was written with stages [{string.Join(", ", reference.Stages.Select(s => s.Name))}] " +
                "but this converter does not support stage reversal.");
        }
 
        await using var stream = await OpenAsync(reference, cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream(reference.Size is > 0 and <= int.MaxValue ? (int)reference.Size : 0);
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
 
        var span = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
 
        if (_options.VerifyIntegrityOnRead)
        {
            var actual = Convert.ToHexString(SHA256.HashData(span)).ToLowerInvariant();
            if (!string.Equals(actual, reference.ContentHash, StringComparison.Ordinal))
            {
                throw new PayloadIntegrityException(reference, actual);
            }
        }
 
        return Encoding.UTF8.GetString(span);
    }
 
    // The only reflective call in the package. If AOT trimming flags it, the fix is for the
    // generated activity invoker to construct PayloadReference<T> itself (it knows T at compile
    // time) rather than asking the converter to produce a closed generic from a Type.
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "PayloadReference<T>.AsTyped is preserved via DynamicDependency on PayloadReference.")]
    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "See above.")]
    private static PayloadReference CoerceReference(PayloadReference reference, Type targetType)
    {
        if (targetType == typeof(PayloadReference))
        {
            return reference;
        }
 
        var elementType = targetType.GetGenericArguments()[0];
        var method = typeof(PayloadReference).GetMethod(nameof(PayloadReference.AsTyped))!.MakeGenericMethod(elementType);
        return (PayloadReference)method.Invoke(reference, null)!;
    }
}
