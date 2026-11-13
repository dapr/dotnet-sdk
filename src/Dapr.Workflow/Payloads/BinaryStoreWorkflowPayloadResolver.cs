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
using System.Threading;
using System.Threading.Tasks;
using Dapr.Common.Serialization;

namespace Dapr.Workflow.Payloads;

/// <summary>Resolves serialized values and stores payloads through the binary-store converter.</summary>
/// <param name="codec">The converter that owns storage, integrity checks, and prefix selection.</param>
/// <param name="serializer">The serializer used for materialized values.</param>
internal sealed class BinaryStoreWorkflowPayloadResolver(
    BinaryStorePayloadConverter codec,
    IDaprSerializer serializer) : IWorkflowPayloadResolver
{
    /// <inheritdoc />
    public async Task<T> ResolveAsync<T>(PayloadReference reference, CancellationToken cancellationToken = default)
    {
        var value = serializer.GetType() == typeof(JsonDaprSerializer)
            ? WorkflowPayloadJsonSerializer.Deserialize<T>((JsonDaprSerializer)serializer,
                (await codec.ResolveBytesAsync(reference, cancellationToken).ConfigureAwait(false)).Span)
            : serializer.Deserialize<T>(await codec.ResolveStringAsync(reference, cancellationToken).ConfigureAwait(false));
        return value
               ?? throw new InvalidOperationException(
                   $"Payload '{reference.Key}' deserialized to null for type {typeof(T)}.");
    }

    /// <inheritdoc />
    public Task<Stream> OpenAsync(PayloadReference reference, CancellationToken cancellationToken = default)
        => codec.OpenAsync(reference, cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<ReadOnlyMemory<byte>> ReadChunksAsync(PayloadReference reference,
        CancellationToken cancellationToken = default)
        => codec.ReadChunksAsync(reference, cancellationToken);

    /// <inheritdoc />
    public Task<PayloadReference> StoreStreamAsync(Stream content, string instanceId,
        CancellationToken cancellationToken = default)
        => codec.StoreStreamAsync(content, instanceId, cancellationToken);

    /// <inheritdoc />
    public Task<PayloadReference> StoreStreamAsync(Stream content, WorkflowPayloadPrefixContext context,
        CancellationToken cancellationToken = default)
        => codec.StoreStreamAsync(content, context, cancellationToken);

    /// <inheritdoc />
    public Task<PayloadReference> WriteAsync(Func<Stream, CancellationToken, Task> write, string instanceId,
        CancellationToken cancellationToken = default)
        => codec.WriteAsync(write, instanceId, cancellationToken);

    /// <inheritdoc />
    public Task<PayloadReference> WriteAsync(Func<Stream, CancellationToken, Task> write, WorkflowPayloadPrefixContext context,
        CancellationToken cancellationToken = default)
        => codec.WriteAsync(write, context, cancellationToken);

    /// <inheritdoc />
    public Task<PayloadReference> UploadAsync(Func<Stream, CancellationToken, Task> write, WorkflowPayloadPrefixContext context,
        CancellationToken cancellationToken = default)
        => WriteAsync(write, context, cancellationToken);

    /// <inheritdoc />
    public Task<PayloadReference> UploadAsync(Stream content, WorkflowPayloadPrefixContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanRead)
            throw new ArgumentException("The payload stream must be readable.", nameof(content));
        return UploadAsync((destination, token) => content.CopyToAsync(destination, token), context, cancellationToken);
    }

    /// <inheritdoc />
    public Task<PayloadReference> UploadAsync(IAsyncEnumerable<ReadOnlyMemory<byte>> chunks, WorkflowPayloadPrefixContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        return UploadAsync(async (destination, token) =>
        {
            await foreach (var chunk in chunks.WithCancellation(token).ConfigureAwait(false))
                await destination.WriteAsync(chunk, token).ConfigureAwait(false);
        }, context, cancellationToken);
    }

    /// <inheritdoc />
    public Task<PayloadReference<T>> StoreAsync<T>(T value, string instanceId,
        CancellationToken cancellationToken = default)
        => StoreAsync(value, new WorkflowPayloadPrefixContext(instanceId), cancellationToken);

    /// <inheritdoc />
    public async Task<PayloadReference<T>> StoreAsync<T>(T value, WorkflowPayloadPrefixContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        PayloadReference reference;
        if (serializer.GetType() == typeof(JsonDaprSerializer))
        {
            var bytes = ((JsonDaprSerializer)serializer).SerializeToUtf8Bytes(value);
            reference = await codec.StoreBytesAsync(bytes, bytes.Length, context, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            reference = await codec.StoreSerializedAsync(serializer.Serialize(value), context, cancellationToken)
                .ConfigureAwait(false);
        }
        return reference.AsTyped<T>();
    }
}
