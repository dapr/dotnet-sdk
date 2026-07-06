// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

namespace Dapr.Client;

/// <summary>
/// gRPC interceptor which adds the required headers for Dapr gRPC proxying.
/// </summary>
/// <param name="appId">The Id of the Dapr Application.</param>
/// <param name="daprApiToken">The api token used for authentication, can be null.</param>
public class InvocationInterceptor(string appId, string daprApiToken) : Interceptor
{
    /// <summary>
    /// Intercept and add headers to a BlockingUnaryCall.
    /// </summary>
    /// <param name="request">The request to intercept.</param>
    /// <param name="context">The client interceptor context to add headers to.</param>
    /// <param name="continuation">The continuation of the request after all headers have been added.</param>
    public override TResponse BlockingUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        AddCallerMetadata(ref context);

        return continuation(request, context);
    }

    /// <summary>
    /// Intercept and add headers to a AsyncUnaryCall.
    /// </summary>
    /// <param name="request">The request to intercept.</param>
    /// <param name="context">The client interceptor context to add headers to.</param>
    /// <param name="continuation">The continuation of the request after all headers have been added.</param>
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        AddCallerMetadata(ref context);

        return continuation(request, context);
    }

    /// <summary>
    /// Intercept and add headers to a AsyncClientStreamingCall.
    /// </summary>
    /// <param name="context">The client interceptor context to add headers to.</param>
    /// <param name="continuation">The continuation of the request after all headers have been added.</param>
    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        AddCallerMetadata(ref context);

        return continuation(context);
    }

    /// <summary>
    /// Intercept and add headers to a AsyncServerStreamingCall.
    /// </summary>
    /// <param name="request">The request to intercept.</param>
    /// <param name="context">The client interceptor context to add headers to.</param>
    /// <param name="continuation">The continuation of the request after all headers have been added.</param>
    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        AddCallerMetadata(ref context);

        return continuation(request, context);
    }

    /// <summary>
    /// Intercept and add headers to a AsyncDuplexStreamingCall.
    /// </summary>
    /// <param name="context">The client interceptor context to add headers to.</param>
    /// <param name="continuation">The continuation of the request after all headers have been added.</param>
    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        AddCallerMetadata(ref context);

        return continuation(context);
    }

    private void AddCallerMetadata<TRequest, TResponse>(ref ClientInterceptorContext<TRequest, TResponse> context)
        where TRequest : class
        where TResponse : class
    {
        var headers = context.Options.Headers;

        // Call doesn't have a headers collection to add to.
        // Need to create a new context with headers for the call.
        if (headers == null)
        {
            headers = [];
            var options = context.Options.WithHeaders(headers);
            context = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, options);
        }

        // Add caller metadata to call headers
        headers.Add("dapr-app-id", appId);
        if (daprApiToken != null)
        {
            headers.Add("dapr-api-token", daprApiToken);
        }            
    }
}
