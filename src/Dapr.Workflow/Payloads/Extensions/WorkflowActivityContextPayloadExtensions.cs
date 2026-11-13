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
using Dapr.Workflow.Payloads;

namespace Dapr.Workflow;

/// <summary>
/// Reads and writes payload references from activities. Writes belong to the current workflow instance.
/// Requires payload support registered with <c>WithBinaryStorePayloads</c>.
/// Methods use the context's worker-shutdown token when no cancelable token is supplied.
/// </summary>
/// <remarks>
/// Writes supply the instance ID, registered activity name, and available owning workflow name
/// to <see cref="BinaryStorePayloadOptions.PrefixFactory"/> through internal context features.
/// Missing workflow names remain null; no metadata lookup is performed.
/// An explicit cancelable token replaces the worker-shutdown default; link tokens if both should apply.
/// </remarks>
public static class WorkflowActivityContextPayloadExtensions
{
    /// <summary>Retrieves and deserializes a typed reference.</summary>
    /// <typeparam name="T">The type described by the reference.</typeparam>
    /// <param name="context">The executing activity context.</param>
    /// <param name="reference">The typed reference to materialize.</param>
    /// <param name="cancellationToken">Cancels resolution; defaults to worker shutdown.</param>
    /// <returns>The deserialized value.</returns>
    /// <exception cref="InvalidOperationException">Payload support is not registered.</exception>
    /// <inheritdoc cref="IWorkflowPayloadResolver.ResolveAsync{T}(PayloadReference, CancellationToken)" path="/exception"/>
    public static Task<T> ResolvePayloadAsync<T>(this WorkflowActivityContext context,
        PayloadReference<T> reference, CancellationToken cancellationToken = default)
        => GetResolver(context).ResolveAsync<T>(reference, GetCancellationToken(context, cancellationToken));

    /// <summary>Retrieves and deserializes an untyped reference.</summary>
    /// <typeparam name="T">The type to deserialize from the stored bytes.</typeparam>
    /// <param name="context">The executing activity context.</param>
    /// <param name="reference">The reference to materialize.</param>
    /// <param name="cancellationToken">Cancels resolution; defaults to worker shutdown.</param>
    /// <returns>The deserialized value.</returns>
    /// <exception cref="InvalidOperationException">Payload support is not registered.</exception>
    /// <inheritdoc cref="IWorkflowPayloadResolver.ResolveAsync{T}(PayloadReference, CancellationToken)" path="/exception"/>
    public static Task<T> ResolvePayloadAsync<T>(this WorkflowActivityContext context,
        PayloadReference reference, CancellationToken cancellationToken = default)
        => GetResolver(context).ResolveAsync<T>(reference, GetCancellationToken(context, cancellationToken));

    /// <summary>
    /// Opens raw stored bytes for reading. The caller owns the stream.
    /// Does not reverse payload stages or verify integrity.
    /// </summary>
    /// <param name="context">The executing activity context.</param>
    /// <param name="reference">The reference to open.</param>
    /// <param name="cancellationToken">Cancels opening and reading; defaults to worker shutdown.</param>
    /// <returns>A caller-owned stream positioned at the start of the stored bytes.</returns>
    /// <exception cref="InvalidOperationException">Payload support is not registered.</exception>
    /// <inheritdoc cref="IWorkflowPayloadResolver.OpenAsync" path="/exception"/>
    public static Task<Stream> OpenPayloadAsync(this WorkflowActivityContext context,
        PayloadReference reference, CancellationToken cancellationToken = default)
        => GetResolver(context).OpenAsync(reference, GetCancellationToken(context, cancellationToken));

    /// <summary>
    /// Enumerates raw stored bytes. Consume each chunk before advancing; memory may be reused.
    /// Does not reverse payload stages or verify integrity.
    /// </summary>
    /// <param name="context">The executing activity context.</param>
    /// <param name="reference">The reference to enumerate.</param>
    /// <param name="cancellationToken">Cancels opening and reading; defaults to worker shutdown.</param>
    /// <returns>A lazy byte sequence whose enumeration owns and disposes the download.</returns>
    /// <exception cref="InvalidOperationException">Payload support is not registered.</exception>
    /// <inheritdoc cref="IWorkflowPayloadResolver.ReadChunksAsync(PayloadReference, CancellationToken)" path="/exception"/>
    public static IAsyncEnumerable<ReadOnlyMemory<byte>> ReadPayloadChunksAsync(this WorkflowActivityContext context,
        PayloadReference reference, CancellationToken cancellationToken = default)
        => GetResolver(context).ReadChunksAsync(reference, GetCancellationToken(context, cancellationToken));

    /// <summary>
    /// Enumerates raw stored bytes in the requested chunk size, except for a shorter final chunk.
    /// Consume each chunk before advancing. Does not reverse payload stages or verify integrity.
    /// </summary>
    /// <param name="context">The executing activity context.</param>
    /// <param name="reference">The reference to enumerate.</param>
    /// <param name="chunkSize">The positive number of bytes per full chunk, not a transport message size.</param>
    /// <param name="cancellationToken">Cancels opening and reading; defaults to worker shutdown.</param>
    /// <returns>A lazy byte sequence whose final chunk may be shorter than the requested size.</returns>
    /// <exception cref="InvalidOperationException">Payload support is not registered.</exception>
    /// <inheritdoc cref="IWorkflowPayloadResolver.ReadChunksAsync(PayloadReference, int, CancellationToken)" path="/exception"/>
    public static IAsyncEnumerable<ReadOnlyMemory<byte>> ReadPayloadChunksAsync(this WorkflowActivityContext context,
        PayloadReference reference, int chunkSize, CancellationToken cancellationToken = default)
        => GetResolver(context).ReadChunksAsync(reference, chunkSize, GetCancellationToken(context, cancellationToken));

    /// <summary>Serializes and stores a value owned by this workflow instance.</summary>
    /// <typeparam name="T">The type of the value to serialize.</typeparam>
    /// <param name="context">The executing activity context, which supplies prefix metadata.</param>
    /// <param name="value">The value to materialize and serialize.</param>
    /// <param name="cancellationToken">Cancels storage and manifest recording; defaults to worker shutdown.</param>
    /// <returns>A typed reference under a content-addressed key within the selected prefix.</returns>
    /// <exception cref="InvalidOperationException">Payload support is not registered.</exception>
    public static Task<PayloadReference<T>> StorePayloadAsync<T>(this WorkflowActivityContext context,
        T value, CancellationToken cancellationToken = default)
        => GetResolver(context).StoreAsync(value, GetPrefixContext(context), GetCancellationToken(context, cancellationToken));

    /// <summary>
    /// Stores remaining source bytes using content-addressed temporary-file staging.
    /// The caller retains ownership of the source. See <see cref="IWorkflowPayloadResolver.StoreStreamAsync(Stream, string, CancellationToken)"/>.
    /// Use <see cref="UploadPayloadAsync(WorkflowActivityContext, Stream, CancellationToken)"/> for immediate upload.
    /// </summary>
    /// <param name="context">The executing activity context, which supplies prefix metadata.</param>
    /// <param name="content">A readable raw-byte source; reads begin at its current position.</param>
    /// <param name="cancellationToken">Cancels staging and upload; defaults to worker shutdown.</param>
    /// <returns>A reference containing the stored byte count and SHA-256 hash.</returns>
    /// <exception cref="InvalidOperationException">Payload support is not registered.</exception>
    public static Task<PayloadReference> StorePayloadStreamAsync(this WorkflowActivityContext context,
        Stream content, CancellationToken cancellationToken = default)
        => GetResolver(context).StoreStreamAsync(content, GetPrefixContext(context), GetCancellationToken(context, cancellationToken));

    /// <summary>
    /// Uploads remaining raw source bytes immediately, without staging to disk.
    /// The caller owns the source. Uses a unique key; retries do not deduplicate.
    /// </summary>
    /// <param name="context">The executing activity context, which supplies prefix metadata.</param>
    /// <param name="content">A readable raw-byte source; reads begin at its current position.</param>
    /// <param name="cancellationToken">Cancels reading and upload; defaults to worker shutdown.</param>
    /// <returns>The completed upload's reference, including its byte count and SHA-256 hash.</returns>
    /// <exception cref="InvalidOperationException">Payload support is not registered.</exception>
    public static Task<PayloadReference> UploadPayloadAsync(this WorkflowActivityContext context,
        Stream content, CancellationToken cancellationToken = default)
        => GetResolver(context).UploadAsync(content, GetPrefixContext(context), GetCancellationToken(context, cancellationToken));

    /// <summary>
    /// Uploads raw byte chunks as they arrive, without local staging or record serialization.
    /// Consumes each chunk before advancing; enumeration is disposed on completion or failure.
    /// </summary>
    /// <param name="context">The executing activity context, which supplies prefix metadata.</param>
    /// <param name="chunks">Already-encoded byte chunks to enumerate once; memory may be reused.</param>
    /// <param name="cancellationToken">Cancels enumeration and upload; defaults to worker shutdown.</param>
    /// <returns>The completed upload's reference, including its byte count and SHA-256 hash.</returns>
    /// <exception cref="InvalidOperationException">Payload support is not registered.</exception>
    public static Task<PayloadReference> UploadPayloadAsync(this WorkflowActivityContext context,
        IAsyncEnumerable<ReadOnlyMemory<byte>> chunks, CancellationToken cancellationToken = default)
        => GetResolver(context).UploadAsync(chunks, GetPrefixContext(context), GetCancellationToken(context, cancellationToken));

    /// <summary>
    /// Produces raw bytes directly into an upload. The SDK owns the destination.
    /// Finish and flush wrappers with leave-open enabled before returning.
    /// All upload overloads use bounded buffering, no temporary file, and unique keys.
    /// </summary>
    /// <param name="context">The executing activity context, which supplies prefix metadata.</param>
    /// <param name="write">The sequential producer, called at most once with an SDK-owned destination and operation token.</param>
    /// <param name="cancellationToken">Cancels production and upload; defaults to worker shutdown.</param>
    /// <returns>A reference after production, upload acknowledgement, and manifest recording succeed.</returns>
    /// <exception cref="InvalidOperationException">Payload support is not registered.</exception>
    public static Task<PayloadReference> UploadPayloadAsync(this WorkflowActivityContext context,
        Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken = default)
        => GetResolver(context).UploadAsync(write, GetPrefixContext(context), GetCancellationToken(context, cancellationToken));

    /// <inheritdoc cref="UploadPayloadAsync(WorkflowActivityContext, Func{Stream, CancellationToken, Task}, CancellationToken)"/>
    /// <summary>
    /// Produces raw bytes directly into an upload with bounded buffering and no temporary file.
    /// The SDK owns the destination; finish and flush wrappers without closing it before returning.
    /// See <see cref="IWorkflowPayloadResolver.WriteAsync(Func{Stream, CancellationToken, Task}, string, CancellationToken)"/> for ownership, retry, and failure semantics.
    /// Compatibility entry point; prefer <c>UploadPayloadAsync</c>.
    /// </summary>
    public static Task<PayloadReference> WritePayloadAsync(this WorkflowActivityContext context,
        Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken = default)
        => GetResolver(context).WriteAsync(write, GetPrefixContext(context), GetCancellationToken(context, cancellationToken));

    /// <summary>Returns whether this context has a payload resolver.</summary>
    /// <param name="context">The activity context to inspect.</param>
    /// <returns>True when payload support is available, otherwise false.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public static bool HasPayloadResolver(this WorkflowActivityContext context) => FindResolver(context) is not null;

    private static IWorkflowPayloadResolver GetResolver(WorkflowActivityContext context)
        => FindResolver(context) ?? throw new InvalidOperationException(
            "Binary store payload support is not registered. Call services.AddDaprWorkflow(...).WithBinaryStorePayloads(...) at startup.");

    private static IWorkflowPayloadResolver? FindResolver(WorkflowActivityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return (context as IWorkflowFeatureSource)?.GetFeature<IWorkflowPayloadResolver>();
    }

    private static CancellationToken GetCancellationToken(WorkflowActivityContext context, CancellationToken token)
        => token.CanBeCanceled ? token : context.CancellationToken;

    private static WorkflowPayloadPrefixContext GetPrefixContext(WorkflowActivityContext context)
        => (context as IWorkflowFeatureSource)?.GetFeature<WorkflowPayloadPrefixContext>()
           ?? new(context.InstanceId, activityName: context.Identifier.Name);
}
