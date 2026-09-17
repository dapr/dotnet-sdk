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
//  ------------------------------------------------------------------------

using System.Collections.Concurrent;
using Dapr.DurableTask.Protobuf;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Dapr.IntegrationTest.Workflow.StatefulHistory;

/// <summary>
/// Counts how the sidecar delivers workflow history over the wire.
/// </summary>
/// <remarks>
/// <para>Mirrors the Go equivalent in dapr's integration framework
/// (<c>tests/integration/framework/process/workflow/worker.go</c>) and the Python and Java SDK
/// observers: every <c>WorkflowRequest</c> arrives either as a delta (<c>CachedHistory</c> set, so
/// <c>PastEvents</c> carries only the events since the worker was last brought up to date) or as a
/// full send, and every <c>GetInstanceHistory</c> call is a cache miss the worker had to recover
/// from.</para>
/// <para>Without this, an end-to-end test cannot tell a working delta path from a sidecar that
/// ignored <c>WORKER_CAPABILITY_STATEFUL_HISTORY</c> altogether: both produce identical workflow
/// output. Counters are concurrent because gRPC delivers on pool threads while the test asserts on
/// its own.</para>
/// </remarks>
internal sealed class WorkItemObserver : Interceptor
{
    private const string GetWorkItemsMethod = "/TaskHubSidecarService/GetWorkItems";

    private readonly ConcurrentDictionary<string, int> _deltas = new();
    private readonly ConcurrentDictionary<string, int> _fullSends = new();
    private readonly ConcurrentDictionary<string, int> _historyFetches = new();

    /// <summary>Work items for this instance whose committed-history prefix the sidecar omitted.</summary>
    public int Deltas(string instanceId) => _deltas.TryGetValue(instanceId, out var n) ? n : 0;

    /// <summary>Work items for this instance carrying the full committed history.</summary>
    public int FullSends(string instanceId) => _fullSends.TryGetValue(instanceId, out var n) ? n : 0;

    /// <summary>GetInstanceHistory calls for this instance, i.e. misses the worker recovered from.</summary>
    public int HistoryFetches(string instanceId) => _historyFetches.TryGetValue(instanceId, out var n) ? n : 0;

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        if (request is GetInstanceHistoryRequest historyRequest)
        {
            Increment(_historyFetches, historyRequest.InstanceId);
        }

        return continuation(request, context);
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var call = continuation(request, context);
        if (!context.Method.FullName.EndsWith(GetWorkItemsMethod, StringComparison.Ordinal))
        {
            return call;
        }

        return new AsyncServerStreamingCall<TResponse>(
            new ObservingStreamReader<TResponse>(call.ResponseStream, this),
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    private void Record(WorkItem workItem)
    {
        if (workItem.WorkflowRequest is null)
        {
            return;
        }

        var request = workItem.WorkflowRequest;
        Increment(request.CachedHistory is not null ? _deltas : _fullSends, request.InstanceId);
    }

    private static void Increment(ConcurrentDictionary<string, int> counters, string instanceId) =>
        counters.AddOrUpdate(instanceId, 1, (_, existing) => existing + 1);

    private sealed class ObservingStreamReader<T>(IAsyncStreamReader<T> inner, WorkItemObserver observer)
        : IAsyncStreamReader<T>
    {
        public T Current => inner.Current;

        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            var moved = await inner.MoveNext(cancellationToken);
            if (moved && inner.Current is WorkItem workItem)
            {
                observer.Record(workItem);
            }

            return moved;
        }
    }
}
