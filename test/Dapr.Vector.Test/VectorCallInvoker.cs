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

namespace Dapr.Vector.Test;

internal sealed class VectorCallInvoker : CallInvoker
{
    public object? Request { get; private set; }
    public string? Method { get; private set; }
    public CallOptions Options { get; private set; }
    public object? Response { get; set; }
    public Func<CallOptions, Task<object>>? Handler { get; set; }
    public int DisposeCount { get; private set; }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
    {
        Request = request;
        Method = method.FullName;
        Options = options;
        return new AsyncUnaryCall<TResponse>(RespondAsync<TResponse>(options), Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess, () => new Metadata(), () => DisposeCount++);
    }

    private async Task<TResponse> RespondAsync<TResponse>(CallOptions options) =>
        (TResponse)(Handler is null ? Response! : await Handler(options));

    public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
        string? host, CallOptions options, TRequest request) => throw new NotSupportedException();

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) =>
        throw new NotSupportedException();

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();
}
