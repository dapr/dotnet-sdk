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

using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Dapr.Workflow.Benchmarks;

/// <summary>Counts protobuf message bodies, not HTTP/2, gRPC framing, TLS, or backend storage traffic.</summary>
internal sealed class MeasuringInterceptor(MeasurementWindow window) : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var measurement = window.Active;
        measurement?.ObserveSent(request);
        var call = continuation(request, context);
        return new AsyncUnaryCall<TResponse>(ObserveAsync(), call.ResponseHeadersAsync,
            call.GetStatus, call.GetTrailers, call.Dispose);

        async Task<TResponse> ObserveAsync()
        {
            var response = await call.ResponseAsync.ConfigureAwait(false);
            measurement?.ObserveReceived(response);
            return response;
        }
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var call = continuation(request, context);
        return new AsyncServerStreamingCall<TResponse>(
            new MeasuringReader<TResponse>(call.ResponseStream, window),
            call.ResponseHeadersAsync, call.GetStatus, call.GetTrailers, call.Dispose);
    }

    private sealed class MeasuringReader<T>(IAsyncStreamReader<T> inner, MeasurementWindow window)
        : IAsyncStreamReader<T> where T : class
    {
        public T Current => inner.Current;
        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            if (!await inner.MoveNext(cancellationToken).ConfigureAwait(false))
                return false;
            window.Active?.ObserveReceived(Current);
            return true;
        }
    }
}
