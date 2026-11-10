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
public static class WorkflowActivityContextPayloadExtensions
{
    /// <summary>Retrieves and deserializes a typed reference.</summary>
    public static Task<T> ResolvePayloadAsync<T>(this WorkflowActivityContext context,
        PayloadReference<T> reference, CancellationToken cancellationToken = default)
        => GetResolver(context).ResolveAsync<T>(reference, GetCancellationToken(context, cancellationToken));

    /// <summary>Retrieves and deserializes an untyped reference.</summary>
    public static Task<T> ResolvePayloadAsync<T>(this WorkflowActivityContext context,
        PayloadReference reference, CancellationToken cancellationToken = default)
        => GetResolver(context).ResolveAsync<T>(reference, GetCancellationToken(context, cancellationToken));

    /// <summary>
    /// Opens raw stored bytes for reading. The caller owns the stream.
    /// Does not reverse payload stages or verify integrity.
    /// </summary>
    public static Task<Stream> OpenPayloadAsync(this WorkflowActivityContext context,
        PayloadReference reference, CancellationToken cancellationToken = default)
        => GetResolver(context).OpenAsync(reference, GetCancellationToken(context, cancellationToken));

    /// <summary>
    /// Enumerates raw stored bytes. Consume each chunk before advancing; memory may be reused.
    /// Does not reverse payload stages or verify integrity.
    /// </summary>
    public static IAsyncEnumerable<ReadOnlyMemory<byte>> ReadPayloadChunksAsync(this WorkflowActivityContext context,
        PayloadReference reference, CancellationToken cancellationToken = default)
        => GetResolver(context).ReadChunksAsync(reference, GetCancellationToken(context, cancellationToken));

    /// <summary>
    /// Enumerates raw stored bytes in the requested chunk size, except for a shorter final chunk.
    /// Consume each chunk before advancing. Does not reverse payload stages or verify integrity.
    /// </summary>
    public static IAsyncEnumerable<ReadOnlyMemory<byte>> ReadPayloadChunksAsync(this WorkflowActivityContext context,
        PayloadReference reference, int chunkSize, CancellationToken cancellationToken = default)
        => GetResolver(context).ReadChunksAsync(reference, chunkSize, GetCancellationToken(context, cancellationToken));

    /// <summary>Serializes and stores a value owned by this workflow instance.</summary>
    public static Task<PayloadReference<T>> StorePayloadAsync<T>(this WorkflowActivityContext context,
        T value, CancellationToken cancellationToken = default)
        => GetResolver(context).StoreAsync(value, context.InstanceId, GetCancellationToken(context, cancellationToken));

    /// <summary>
    /// Stores remaining source bytes using content-addressed temporary-file staging.
    /// The caller retains ownership of the source. See <see cref="IWorkflowPayloadResolver.StoreStreamAsync"/>.
    /// Use <see cref="UploadPayloadAsync(WorkflowActivityContext, Stream, CancellationToken)"/> for immediate upload.
    /// </summary>
    public static Task<PayloadReference> StorePayloadStreamAsync(this WorkflowActivityContext context,
        Stream content, CancellationToken cancellationToken = default)
        => GetResolver(context).StoreStreamAsync(content, context.InstanceId, GetCancellationToken(context, cancellationToken));

    /// <summary>
    /// Uploads remaining raw source bytes immediately, without staging to disk.
    /// The caller owns the source. Uses a unique key; retries do not deduplicate.
    /// </summary>
    public static Task<PayloadReference> UploadPayloadAsync(this WorkflowActivityContext context,
        Stream content, CancellationToken cancellationToken = default)
        => GetResolver(context).UploadAsync(content, context.InstanceId, GetCancellationToken(context, cancellationToken));

    /// <summary>
    /// Uploads raw byte chunks as they arrive, without local staging or record serialization.
    /// Consumes each chunk before advancing; enumeration is disposed on completion or failure.
    /// </summary>
    public static Task<PayloadReference> UploadPayloadAsync(this WorkflowActivityContext context,
        IAsyncEnumerable<ReadOnlyMemory<byte>> chunks, CancellationToken cancellationToken = default)
        => GetResolver(context).UploadAsync(chunks, context.InstanceId, GetCancellationToken(context, cancellationToken));

    /// <summary>
    /// Produces raw bytes directly into an upload. The SDK owns the destination.
    /// Finish and flush wrappers with leave-open enabled before returning.
    /// All upload overloads use bounded buffering, no temporary file, and unique keys.
    /// </summary>
    public static Task<PayloadReference> UploadPayloadAsync(this WorkflowActivityContext context,
        Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken = default)
        => GetResolver(context).UploadAsync(write, context.InstanceId, GetCancellationToken(context, cancellationToken));

    /// <summary>
    /// Produces raw bytes directly into an upload with bounded buffering and no temporary file.
    /// The SDK owns the destination; finish and flush wrappers without closing it before returning.
    /// See <see cref="IWorkflowPayloadResolver.WriteAsync"/> for ownership, retry, and failure semantics.
    /// Compatibility entry point; prefer <c>UploadPayloadAsync</c>.
    /// </summary>
    public static Task<PayloadReference> WritePayloadAsync(this WorkflowActivityContext context,
        Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken = default)
        => GetResolver(context).WriteAsync(write, context.InstanceId, GetCancellationToken(context, cancellationToken));

    /// <summary>Returns whether this context has a payload resolver.</summary>
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
}
