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

using System;
using System.Net.Http;
using Grpc.Core;

namespace Dapr.Client;

/// <summary>
/// The exception type thrown when an exception is encountered using Dapr service invocation.
/// </summary>
public class InvocationException : DaprException
{
    /// <summary>
    /// Initializes a new <see cref="InvocationException" /> for a non-successful HTTP request.
    /// </summary>
    public InvocationException(string appId, string methodName, Exception innerException, HttpResponseMessage response)
        : base(FormatExceptionForFailedRequest(appId, methodName), innerException)
    {
        this.AppId = appId ?? "unknown";
        this.MethodName = methodName ?? "unknown";
        this.Response = response;
    }

    /// <summary>
    /// Initializes a new <see cref="InvocationException" /> for a non-successful gRPC request.
    /// </summary>
    public InvocationException(string appId, string methodName, RpcException innerException)
        : base(FormatExceptionForFailedRequest(appId, methodName), innerException)
    {
        this.AppId = appId ?? "unknown";
        this.MethodName = methodName ?? "unknown";
    }

    /// <summary>
    /// Gets the destination app-id of the invocation request that failed.
    /// </summary>
    public string AppId { get; }

    /// <summary>
    /// Gets the destination method name of the invocation request that failed.
    /// </summary>
    public string MethodName { get; }

    /// <summary>
    /// Gets the <see cref="HttpResponseMessage" /> of the request that failed. Will be <c>null</c> if the 
    /// failure was not related to an HTTP request or preventing the response from being recieved.
    /// </summary>
    public HttpResponseMessage Response { get; }

    private static string FormatExceptionForFailedRequest(string appId, string methodName)
    {
        return $"An exception occurred while invoking method: '{methodName}' on app-id: '{appId}'";
    }
}