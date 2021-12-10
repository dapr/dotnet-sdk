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
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;

namespace Dapr.Client
{
    /// <summary>
    /// <para>
    /// Defines client methods for interacting with Dapr endpoints.
    /// Use <see cref="DaprClientBuilder"/> to create <see cref="DaprClient"/>.
    /// </para>
    /// <para>
    /// Implementations of <see cref="DaprClient" /> implement <see cref="IDisposable" /> because the client
    /// accesses network resources. For best performance, create a single long-lived client instance and share
    /// it for the lifetime of the application. Avoid creating and disposing a client instance for each operation
    /// that the application performs - this can lead to socket exhaustion and other problems.
    /// </para>
    /// </summary>
    public abstract class DaprClient : IDisposable
    {
        private bool disposed;

        /// <summary>
        /// Gets the <see cref="JsonSerializerOptions" /> used for JSON serialization operations.
        /// </summary>
        public abstract JsonSerializerOptions JsonSerializerOptions { get; }

        /// <summary>
        /// <para>
        /// Creates an <see cref="HttpClient" /> that can be used to perform Dapr service
        /// invocation using <see cref="HttpRequestMessage" /> objects.
        /// </para>
        /// <para>
        /// The client will read the <see cref="HttpRequestMessage.RequestUri" /> property, and 
        /// interpret the hostname as the destination <c>app-id</c>. The <see cref="HttpRequestMessage.RequestUri" /> 
        /// property will be replaced with a new URI with the authority section replaced by <paramref name="daprEndpoint" />
        /// and the path portion of the URI rewitten to follow the format of a Dapr service invocation request.
        /// </para>
        /// </summary>
        /// <param name="appId">
        /// An optional <c>app-id</c>. If specified, the <c>app-id</c> will be configured as the value of 
        /// <see cref="HttpClient.BaseAddress" /> so that relative URIs can be used.
        /// </param>
        /// <param name="daprEndpoint">The HTTP endpoint of the Dapr process to use for service invocation calls.</param>
        /// <param name="daprApiToken">The token to be added to all request headers to Dapr runtime.</param>
        /// <returns>An <see cref="HttpClient" /> that can be used to perform service invocation requests.</returns>
        /// <remarks>
        /// <para>
        /// The <see cref="HttpClient" /> object is intended to be a long-lived and holds access to networking resources.
        /// Since the value of <paramref name="daprEndpoint" /> will not change during the lifespan of the application,
        /// a single client object can be reused for the life of the application.
        /// </para>
        /// </remarks>
        public static HttpClient CreateInvokeHttpClient(string appId = null, string daprEndpoint = null, string daprApiToken = null)
        {
            var handler = new InvocationHandler()
            {
                InnerHandler = new HttpClientHandler(),
                DaprApiToken = daprApiToken
            };

            if (daprEndpoint is string)
            {
                // DaprEndpoint performs validation.
                handler.DaprEndpoint = daprEndpoint;
            }

            var httpClient = new HttpClient(handler);

            if (appId is string)
            {
                try
                {
                    httpClient.BaseAddress = new Uri($"http://{appId}");
                }
                catch (UriFormatException inner)
                {
                    throw new ArgumentException("The appId must be a valid hostname.", nameof(appId), inner);
                }
            }

            return httpClient;
        }

        /// <summary>
        /// <para>
        /// Creates an <see cref="CallInvoker"/> that can be used to perform locally defined gRPC calls 
        /// using the Dapr sidecar as a proxy.
        /// </para>
        /// <para>
        /// The created <see cref="CallInvoker"/> is used to intercept a <see cref="GrpcChannel"/> with an
        /// <see cref="InvocationInterceptor"/>. The interceptor inserts the <paramref name="appId"/> and, if present,
        /// the <paramref name="daprApiToken"/> into the request's metadata.
        /// </para>
        /// </summary>
        /// <param name="appId">
        /// The appId that is targetted by Dapr for gRPC invocations.
        /// </param>
        /// <param name="daprEndpoint">
        /// Optional gRPC endpoint for calling Dapr, defaults to <see cref="DaprDefaults.GetDefaultGrpcEndpoint"/>.
        /// </param>
        /// <param name="daprApiToken">
        /// Optional token to be attached to all requests, defaults to <see cref="DaprDefaults.GetDefaultApiToken"/>.
        /// </param>
        /// <returns>An <see cref="CallInvoker"/> to be used for proxied gRPC calls through Dapr.</returns>
        /// <remarks>
        /// <para>
        /// As the <paramref name="daprEndpoint"/> will remain constant, a single instance of the
        /// <see cref="CallInvoker"/> can be used throughout the lifetime of the application.
        /// </para>
        /// </remarks>
        public static CallInvoker CreateInvocationInvoker(string appId, string daprEndpoint = null, string daprApiToken = null)
        {
            var channel = GrpcChannel.ForAddress(daprEndpoint ?? DaprDefaults.GetDefaultGrpcEndpoint());
            return channel.Intercept(new InvocationInterceptor(appId, daprApiToken ?? DaprDefaults.GetDefaultApiToken()));
        }

        internal static KeyValuePair<string, string>? GetDaprApiTokenHeader(string apiToken)
        {
            if (string.IsNullOrWhiteSpace(apiToken))
            {
                return null;
            }

            return new KeyValuePair<string, string>("dapr-api-token", apiToken);
        }

        /// <summary>
        /// Publishes an event to the specified topic.
        /// </summary>
        /// <param name="pubsubName">The name of the pubsub component to use.</param>
        /// <param name="topicName">The name of the topic the request should be published to.</param>
        /// <param name="data">The data that will be JSON serialized and provided as the event payload.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TData">The type of the data that will be JSON serialized and provided as the event payload.</typeparam>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.</returns>
        public abstract Task PublishEventAsync<TData>(
            string pubsubName,
            string topicName,
            TData data,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Publishes an event to the specified topic.
        /// </summary>
        /// <param name="pubsubName">The name of the pubsub component to use.</param>
        /// <param name="topicName">The name of the topic the request should be published to.</param>
        /// <param name="data">The data that will be JSON serialized and provided as the event payload.</param>
        /// <param name="metadata">
        /// A collection of metadata key-value pairs that will be provided to the pubsub. The valid metadata keys and values 
        /// are determined by the type of pubsub component used.
        /// </param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TData">The type of the data that will be JSON serialized and provided as the event payload.</typeparam>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.</returns>
        public abstract Task PublishEventAsync<TData>(
            string pubsubName,
            string topicName,
            TData data,
            Dictionary<string, string> metadata,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Publishes an event to the specified topic.
        /// </summary>
        /// <param name="pubsubName">The name of the pubsub component to use.</param>
        /// <param name="topicName">The name of the topic the request should be published to.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.</returns>
        public abstract Task PublishEventAsync(
            string pubsubName,
            string topicName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Publishes an event to the specified topic.
        /// </summary>
        /// <param name="pubsubName">The name of the pubsub component to use.</param>
        /// <param name="topicName">The name of the topic the request should be published to.</param>
        /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the pubsub. The valid metadata keys and values are determined by the type of binding used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.</returns>
        public abstract Task PublishEventAsync(
            string pubsubName,
            string topicName,
            Dictionary<string, string> metadata,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Invokes an output binding.
        /// </summary>
        /// <typeparam name="TRequest">The type of the data that will be JSON serialized and provided as the binding payload.</typeparam>
        /// <param name="bindingName">The name of the binding to sent the event to.</param>
        /// <param name="operation">The type of operation to perform on the binding.</param>
        /// <param name="data">The data that will be JSON serialized and provided as the binding payload.</param>
        /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the binding. The valid metadata keys and values are determined by the type of binding used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.</returns>
        public abstract Task InvokeBindingAsync<TRequest>(
            string bindingName,
            string operation,
            TRequest data,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Invokes an output binding.
        /// </summary>
        /// <typeparam name="TRequest">The type of the data that will be JSON serialized and provided as the binding payload.</typeparam>
        /// <typeparam name="TResponse">The type of the data that will be JSON deserialized from the binding response.</typeparam>
        /// <param name="bindingName">The name of the binding to sent the event to.</param>
        /// <param name="operation">The type of operation to perform on the binding.</param>
        /// <param name="data">The data that will be JSON serialized and provided as the binding payload.</param>
        /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the binding. The valid metadata keys and values are determined by the type of binding used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{T}" /> that will complete when the operation has completed.</returns>
        public abstract Task<TResponse> InvokeBindingAsync<TRequest, TResponse>(
            string bindingName,
            string operation,
            TRequest data,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Invokes a binding with the provided <paramref name="request" />. This method allows for control of the binding
        /// input and output using raw bytes.
        /// </summary>
        /// <param name="request">The <see cref="BindingRequest" /> to send.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{T}" /> that will complete when the operation has completed.</returns>
        public abstract Task<BindingResponse> InvokeBindingAsync(
            BindingRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates an <see cref="HttpRequestMessage" /> that can be used to perform service invocation for the
        /// application idenfied by <paramref name="appId" /> and invokes the method specified by <paramref name="methodName" />
        /// with the <c>POST</c> HTTP method.
        /// </summary>
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <returns>An <see cref="HttpRequestMessage" /> for use with <c>SendInvokeMethodRequestAsync</c>.</returns>
        public HttpRequestMessage CreateInvokeMethodRequest(string appId, string methodName)
        {
            return CreateInvokeMethodRequest(HttpMethod.Post, appId, methodName);
        }

        /// <summary>
        /// Creates an <see cref="HttpRequestMessage" /> that can be used to perform service invocation for the
        /// application idenfied by <paramref name="appId" /> and invokes the method specified by <paramref name="methodName" />
        /// with the HTTP method specified by <paramref name="httpMethod" />.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod" /> to use for the invocation request.</param>
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <returns>An <see cref="HttpRequestMessage" /> for use with <c>SendInvokeMethodRequestAsync</c>.</returns>
        public abstract HttpRequestMessage CreateInvokeMethodRequest(HttpMethod httpMethod, string appId, string methodName);

        /// <summary>
        /// Creates an <see cref="HttpRequestMessage" /> that can be used to perform service invocation for the
        /// application idenfied by <paramref name="appId" /> and invokes the method specified by <paramref name="methodName" />
        /// with the <c>POST</c> HTTP method and a JSON serialized request body specified by <paramref name="data" />.
        /// </summary>
        /// <typeparam name="TRequest">The type of the data that will be JSON serialized and provided as the request body.</typeparam>
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="data">The data that will be JSON serialized and provided as the request body.</param>
        /// <returns>An <see cref="HttpRequestMessage" /> for use with <c>SendInvokeMethodRequestAsync</c>.</returns>
        public HttpRequestMessage CreateInvokeMethodRequest<TRequest>(string appId, string methodName, TRequest data)
        {
            return CreateInvokeMethodRequest<TRequest>(HttpMethod.Post, appId, methodName, data);
        }

        /// <summary>
        /// Creates an <see cref="HttpRequestMessage" /> that can be used to perform service invocation for the
        /// application idenfied by <paramref name="appId" /> and invokes the method specified by <paramref name="methodName" />
        /// with the HTTP method specified by <paramref name="httpMethod" /> and a JSON serialized request body specified by 
        /// <paramref name="data" />.
        /// </summary>
        /// <typeparam name="TRequest">The type of the data that will be JSON serialized and provided as the request body.</typeparam>
        /// <param name="httpMethod">The <see cref="HttpMethod" /> to use for the invocation request.</param>
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="data">The data that will be JSON serialized and provided as the request body.</param>
        /// <returns>An <see cref="HttpRequestMessage" /> for use with <c>SendInvokeMethodRequestAsync</c>.</returns>
        public abstract HttpRequestMessage CreateInvokeMethodRequest<TRequest>(HttpMethod httpMethod, string appId, string methodName, TRequest data);
        
        /// <summary>
        /// Perform health-check of Dapr sidecar. Return 'true' if sidecar is healthy. Otherwise 'false'.
        /// CheckHealthAsync handle <see cref="HttpRequestException"/> and will return 'false' if error will occur on transport level
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{T}" /> that will return the value when the operation has completed.</returns>
        public abstract Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Perform service invocation using the request provided by <paramref name="request" />. The response will
        /// be returned without performing any validation on the status code.
        /// </summary>
        /// <param name="request">
        /// The <see cref="HttpRequestMessage" /> to send. The request must be a conforming Dapr service invocation request. 
        /// Use the <c>CreateInvokeMethodRequest</c> to create service invocation requests.
        /// </param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{T}" /> that will return the value when the operation has completed.</returns>
        public abstract Task<HttpResponseMessage> InvokeMethodWithResponseAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Perform service invocation using the request provided by <paramref name="request" />. If the response has a non-success
        /// status an exception will be thrown.
        /// </summary>
        /// <param name="request">
        /// The <see cref="HttpRequestMessage" /> to send. The request must be a conforming Dapr service invocation request. 
        /// Use the <c>CreateInvokeMethodRequest</c> to create service invocation requests.
        /// </param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will return when the operation has completed.</returns>
        public abstract Task InvokeMethodAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Perform service invocation using the request provided by <paramref name="request" />. If the response has a success
        /// status code the body will be deserialized using JSON to a value of type <typeparamref name="TResponse" />;
        /// otherwise an exception will be thrown.
        /// </summary>
        /// <typeparam name="TResponse">The type of the data that will be JSON deserialized from the response body.</typeparam>
        /// <param name="request">
        /// The <see cref="HttpRequestMessage" /> to send. The request must be a conforming Dapr service invocation request. 
        /// Use the <c>CreateInvokeMethodRequest</c> to create service invocation requests.
        /// </param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{T}" /> that will return the value when the operation has completed.</returns>
        public abstract Task<TResponse> InvokeMethodAsync<TResponse>(HttpRequestMessage request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Perform service invocation for the application idenfied by <paramref name="appId" /> and invokes the method 
        /// specified by <paramref name="methodName" /> with the <c>POST</c> HTTP method and an empty request body. 
        /// If the response has a non-success status code an exception will be thrown.
        /// </summary>
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will return when the operation has completed.</returns>
        public Task InvokeMethodAsync(
            string appId,
            string methodName,
            CancellationToken cancellationToken = default)
        {
            var request = CreateInvokeMethodRequest(appId, methodName);
            return InvokeMethodAsync(request, cancellationToken);
        }

        /// <summary>
        /// Perform service invocation for the application idenfied by <paramref name="appId" /> and invokes the method 
        /// specified by <paramref name="methodName" /> with the HTTP method specified by <paramref name="methodName" />
        /// and an empty request body. If the response has a non-success status code an exception will be thrown.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod" /> to use for the invocation request.</param>
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will return when the operation has completed.</returns>
        public Task InvokeMethodAsync(
            HttpMethod httpMethod,
            string appId,
            string methodName,
            CancellationToken cancellationToken = default)
        {
            var request = CreateInvokeMethodRequest(httpMethod, appId, methodName);
            return InvokeMethodAsync(request, cancellationToken);
        }

        /// <summary>
        /// Perform service invocation for the application idenfied by <paramref name="appId" /> and invokes the method 
        /// specified by <paramref name="methodName" /> with the <c>POST</c> HTTP method
        /// and a JSON serialized request body specified by <paramref name="data" />. If the response has a non-success
        /// status code an exception will be thrown.
        /// </summary>
        /// <typeparam name="TRequest">The type of the data that will be JSON serialized and provided as the request body.</typeparam>
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="data">The data that will be JSON serialized and provided as the request body.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will return when the operation has completed.</returns>
        public Task InvokeMethodAsync<TRequest>(
            string appId,
            string methodName,
            TRequest data,
            CancellationToken cancellationToken = default)
        {
            var request = CreateInvokeMethodRequest<TRequest>(appId, methodName, data);
            return InvokeMethodAsync(request, cancellationToken);
        }

        /// <summary>
        /// Perform service invocation for the application idenfied by <paramref name="appId" /> and invokes the method 
        /// specified by <paramref name="methodName" /> with the HTTP method specified by <paramref name="httpMethod" /> 
        /// and a JSON serialized request body specified by <paramref name="data" />. If the response has a non-success
        /// status code an exception will be thrown.
        /// </summary>
        /// <typeparam name="TRequest">The type of the data that will be JSON serialized and provided as the request body.</typeparam>
        /// <param name="httpMethod">The <see cref="HttpMethod" /> to use for the invocation request.</param>
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="data">The data that will be JSON serialized and provided as the request body.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will return when the operation has completed.</returns>
        public Task InvokeMethodAsync<TRequest>(
            HttpMethod httpMethod,
            string appId,
            string methodName,
            TRequest data,
            CancellationToken cancellationToken = default)
        {
            var request = CreateInvokeMethodRequest<TRequest>(httpMethod, appId, methodName, data);
            return InvokeMethodAsync(request, cancellationToken);
        }

        /// <summary>
        /// Perform service invocation for the application idenfied by <paramref name="appId" /> and invokes the method 
        /// specified by <paramref name="methodName" /> with the <c>POST</c> HTTP method
        /// and an empty request body. If the response has a success
        /// status code the body will be deserialized using JSON to a value of type <typeparamref name="TResponse" />;
        /// otherwise an exception will be thrown.
        /// </summary>
        /// <typeparam name="TResponse">The type of the data that will be JSON deserialized from the response body.</typeparam>
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{T}" /> that will return the value when the operation has completed.</returns>
        public Task<TResponse> InvokeMethodAsync<TResponse>(
            string appId,
            string methodName,
            CancellationToken cancellationToken = default)
        {
            var request = CreateInvokeMethodRequest(appId, methodName);
            return InvokeMethodAsync<TResponse>(request, cancellationToken);
        }

        /// <summary>
        /// Perform service invocation for the application idenfied by <paramref name="appId" /> and invokes the method 
        /// specified by <paramref name="methodName" /> with the HTTP method specified by <paramref name="httpMethod" /> 
        /// and an empty request body. If the response has a success
        /// status code the body will be deserialized using JSON to a value of type <typeparamref name="TResponse" />;
        /// otherwise an exception will be thrown.
        /// </summary>
        /// <typeparam name="TResponse">The type of the data that will be JSON deserialized from the response body.</typeparam>
        /// <param name="httpMethod">The <see cref="HttpMethod" /> to use for the invocation request.</param>
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{T}" /> that will return the value when the operation has completed.</returns>
        public Task<TResponse> InvokeMethodAsync<TResponse>(
            HttpMethod httpMethod,
            string appId,
            string methodName,
            CancellationToken cancellationToken = default)
        {
            var request = CreateInvokeMethodRequest(httpMethod, appId, methodName);
            return InvokeMethodAsync<TResponse>(request, cancellationToken);
        }

        /// <summary>
        /// Perform service invocation for the application idenfied by <paramref name="appId" /> and invokes the method 
        /// specified by <paramref name="methodName" /> with the <c>POST</c> HTTP method
        /// and a JSON serialized request body specified by <paramref name="data" />. If the response has a success
        /// status code the body will be deserialized using JSON to a value of type <typeparamref name="TResponse" />;
        /// otherwise an exception will be thrown.
        /// </summary>
        /// <typeparam name="TRequest">The type of the data that will be JSON serialized and provided as the request body.</typeparam>
        /// <typeparam name="TResponse">The type of the data that will be JSON deserialized from the response body.</typeparam>
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="data">The data that will be JSON serialized and provided as the request body.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{T}" /> that will return the value when the operation has completed.</returns>
        public Task<TResponse> InvokeMethodAsync<TRequest, TResponse>(
            string appId,
            string methodName,
            TRequest data,
            CancellationToken cancellationToken = default)
        {
            var request = CreateInvokeMethodRequest<TRequest>(appId, methodName, data);
            return InvokeMethodAsync<TResponse>(request, cancellationToken);
        }

        /// <summary>
        /// Perform service invocation for the application idenfied by <paramref name="appId" /> and invokes the method 
        /// specified by <paramref name="methodName" /> with the HTTP method specified by <paramref name="httpMethod" /> 
        /// and a JSON serialized request body specified by <paramref name="data" />. If the response has a success
        /// status code the body will be deserialized using JSON to a value of type <typeparamref name="TResponse" />;
        /// otherwise an exception will be thrown.
        /// </summary>
        /// <typeparam name="TRequest">The type of the data that will be JSON serialized and provided as the request body.</typeparam>
        /// <typeparam name="TResponse">The type of the data that will be JSON deserialized from the response body.</typeparam>
        /// <param name="httpMethod">The <see cref="HttpMethod" /> to use for the invocation request.</param>
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="data">The data that will be JSON serialized and provided as the request body.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{T}" /> that will return the value when the operation has completed.</returns>
        public Task<TResponse> InvokeMethodAsync<TRequest, TResponse>(
            HttpMethod httpMethod,
            string appId,
            string methodName,
            TRequest data,
            CancellationToken cancellationToken = default)
        {
            var request = CreateInvokeMethodRequest<TRequest>(httpMethod, appId, methodName, data);
            return InvokeMethodAsync<TResponse>(request, cancellationToken);
        }

        /// <summary>
        /// Perform service invocation using gRPC semantics for the application idenfied by <paramref name="appId" /> and invokes the method 
        /// specified by <paramref name="methodName" /> with an empty request body. 
        /// If the response has a non-success status code an exception will be thrown.
        /// </summary>
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will return when the operation has completed.</returns>
        public abstract Task InvokeMethodGrpcAsync(
            string appId,
            string methodName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Perform service invocation using gRPC semantics for the application idenfied by <paramref name="appId" /> and invokes the method 
        /// specified by <paramref name="methodName" /> with a Protobuf serialized request body specified by <paramref name="data" />.
        /// If the response has a non-success status code an exception will be thrown.
        /// </summary>
        /// <typeparam name="TRequest">The type of the data that will be Protobuf serialized and provided as the request body.</typeparam>
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="data">The data that will be Protobuf serialized and provided as the request body.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will return when the operation has completed.</returns>
        public abstract Task InvokeMethodGrpcAsync<TRequest>(
            string appId,
            string methodName,
            TRequest data,
            CancellationToken cancellationToken = default)
        where TRequest : IMessage;

        /// <summary>
        /// Perform service invocation using gRPC semantics for the application idenfied by <paramref name="appId" /> and invokes the method 
        /// specified by <paramref name="methodName" /> with an empty request body. If the response has a success
        /// status code the body will be deserialized using Protobuf to a value of type <typeparamref name="TResponse" />;
        /// otherwise an exception will be thrown.
        /// </summary>
        /// <typeparam name="TResponse">The type of the data that will be Protobuf deserialized from the response body.</typeparam>
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{T}" /> that will return the value when the operation has completed.</returns>
        public abstract Task<TResponse> InvokeMethodGrpcAsync<TResponse>(
            string appId,
            string methodName,
            CancellationToken cancellationToken = default)
        where TResponse : IMessage, new();

        /// <summary>
        /// Perform service invocation using gRPC semantics for the application idenfied by <paramref name="appId" /> and invokes the method 
        /// specified by <paramref name="methodName" /> with a Protobuf serialized request body specified by <paramref name="data" />. If the response has a success
        /// status code the body will be deserialized using Protobuf to a value of type <typeparamref name="TResponse" />;
        /// otherwise an exception will be thrown.
        /// </summary>
        /// <typeparam name="TRequest">The type of the data that will be Protobuf serialized and provided as the request body.</typeparam>
        /// <typeparam name="TResponse">The type of the data that will be Protobuf deserialized from the response body.</typeparam>
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="data">The data that will be Protobuf serialized and provided as the request body.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{T}" /> that will return the value when the operation has completed.</returns>
        public abstract Task<TResponse> InvokeMethodGrpcAsync<TRequest, TResponse>(
            string appId,
            string methodName,
            TRequest data,
            CancellationToken cancellationToken = default)
        where TRequest : IMessage
        where TResponse : IMessage, new();

        /// <summary>
        /// Gets the current value associated with the <paramref name="key" /> from the Dapr state store.
        /// </summary>
        /// <param name="storeName">The name of state store to read from.</param>
        /// <param name="key">The state key.</param>
        /// <param name="consistencyMode">The consistency mode <see cref="ConsistencyMode" />.</param>
        /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the state store. The valid metadata keys and values are determined by the type of state store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The data type of the value to read.</typeparam>
        /// <returns>A <see cref="Task{T}" /> that will return the value when the operation has completed.</returns>
        public abstract Task<TValue> GetStateAsync<TValue>(string storeName, string key, ConsistencyMode? consistencyMode = default, IReadOnlyDictionary<string, string> metadata = default, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a list of values associated with the <paramref name="keys" /> from the Dapr state store.
        /// </summary>
        /// <param name="storeName">The name of state store to read from.</param>
        /// <param name="keys">The list of keys to get values for.</param>
        /// <param name="parallelism">The number of concurrent get operations the Dapr runtime will issue to the state store. a value equal to or smaller than 0 means max parallelism.</param>
        /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the state store. The valid metadata keys and values are determined by the type of state store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{IReadOnlyList}" /> that will return the list of values when the operation has completed.</returns>
        public abstract Task<IReadOnlyList<BulkStateItem>> GetBulkStateAsync(string storeName, IReadOnlyList<string> keys, int? parallelism, IReadOnlyDictionary<string, string> metadata = default, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a list of <paramref name="items" /> from the Dapr state store.
        /// </summary>
        /// <param name="storeName">The name of state store to delete from.</param>
        /// <param name="items">The list of items to delete</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.</returns>
        public abstract Task DeleteBulkStateAsync(string storeName, IReadOnlyList<BulkDeleteStateItem> items, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current value associated with the <paramref name="key" /> from the Dapr state store and an ETag.
        /// </summary>
        /// <typeparam name="TValue">The data type of the value to read.</typeparam>
        /// <param name="storeName">The name of the state store.</param>
        /// <param name="key">The state key.</param>
        /// <param name="consistencyMode">The consistency mode <see cref="ConsistencyMode" />.</param>
        /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the state store. The valid metadata keys and values are determined by the type of state store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{T}" /> that will return the value when the operation has completed.  This wraps the read value and an ETag.</returns>
        public abstract Task<(TValue value, string etag)> GetStateAndETagAsync<TValue>(string storeName, string key, ConsistencyMode? consistencyMode = default, IReadOnlyDictionary<string, string> metadata = default, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a <see cref="StateEntry{T}" /> for the current value associated with the <paramref name="key" /> from
        /// the Dapr state store.
        /// </summary>
        /// <param name="storeName">The name of the state store.</param>
        /// <param name="key">The state key.</param>
        /// <param name="consistencyMode">The consistency mode <see cref="ConsistencyMode" />.</param>
        /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the state store. The valid metadata keys and values are determined by the type of state store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The type of the data that will be JSON deserialized from the state store response.</typeparam>
        /// <returns>A <see cref="Task" /> that will return the <see cref="StateEntry{T}" /> when the operation has completed.</returns>
        public async Task<StateEntry<TValue>> GetStateEntryAsync<TValue>(string storeName, string key, ConsistencyMode? consistencyMode = default, IReadOnlyDictionary<string, string> metadata = default, CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

            var (state, etag) = await this.GetStateAndETagAsync<TValue>(storeName, key, consistencyMode, metadata, cancellationToken);
            return new StateEntry<TValue>(this, storeName, key, state, etag);
        }

        /// <summary>
        /// Saves the provided <paramref name="value" /> associated with the provided <paramref name="key" /> to the Dapr state
        /// store.
        /// </summary>
        /// <param name="storeName">The name of the state store.</param>
        /// <param name="key">The state key.</param>
        /// <param name="value">The data that will be JSON serialized and stored in the state store.</param>        
        /// <param name="stateOptions">Options for performing save state operation.</param>
        /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the state store. The valid metadata keys and values are determined by the type of state store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The type of the data that will be JSON serialized and stored in the state store.</typeparam>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.</returns>
        public abstract Task SaveStateAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            StateOptions stateOptions = default,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Tries to save the state <paramref name="value" /> associated with the provided <paramref name="key" /> using the 
        /// <paramref name="etag"/> to the Dapr state. State store implementation will allow the update only if the attached ETag matches with the latest ETag in the state store.
        /// store.
        /// </summary>
        /// <param name="storeName">The name of the state store.</param>
        /// <param name="key">The state key.</param>
        /// <param name="value">The data that will be JSON serialized and stored in the state store.</param>
        /// <param name="etag">An ETag.</param>
        /// <param name="stateOptions">Options for performing save state operation.</param>
        /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the state store. The valid metadata keys and values are determined by the type of state store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The type of the data that will be JSON serialized and stored in the state store.</typeparam>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.  If the wrapped value is true the operation succeeded.</returns>
        public abstract Task<bool> TrySaveStateAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            string etag,
            StateOptions stateOptions = default,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves the provided <paramref name="operations" /> to the Dapr state
        /// store.
        /// </summary>
        /// <param name="storeName">The name of the state store.</param>
        /// <param name="operations">A list of StateTransactionRequests.</param>
        /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the state store. The valid metadata keys and values are determined by the type of state store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.</returns>
        public abstract Task ExecuteStateTransactionAsync(
            string storeName,
            IReadOnlyList<StateTransactionRequest> operations,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes the value associated with the provided <paramref name="key" /> in the Dapr state store.
        /// </summary>
        /// <param name="storeName">The state store name.</param>
        /// <param name="key">The state key.</param>
        /// <param name="stateOptions">A <see cref="StateOptions" />.</param>
        /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the state store. The valid metadata keys and values are determined by the type of state store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.</returns>
        public abstract Task DeleteStateAsync(
            string storeName,
            string key,
            StateOptions stateOptions = default,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Tries to delete the the state associated with the provided <paramref name="key" /> using the 
        /// <paramref name="etag"/> from the Dapr state. State store implementation will allow the delete only if the attached ETag matches with the latest ETag in the state store.
        /// </summary>
        /// <param name="storeName">The state store name.</param>
        /// <param name="key">The state key.</param>
        /// <param name="etag">An ETag.</param>
        /// <param name="stateOptions">A <see cref="StateOptions" />.</param>
        /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the state store. The valid metadata keys and values are determined by the type of state store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.  If the wrapped value is true the operation suceeded.</returns>
        public abstract Task<bool> TryDeleteStateAsync(
            string storeName,
            string key,
            string etag,
            StateOptions stateOptions = default,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the secret value from the secret store.
        /// </summary>
        /// <param name="storeName">Secret store name.</param>
        /// <param name="key">Key for the secret.</param>
        /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the secret store. The valid metadata keys and values are determined by the type of secret store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{T}" /> that will return the value when the operation has completed.</returns>
        public abstract Task<Dictionary<string, string>> GetSecretAsync(
            string storeName,
            string key,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all secret values that the application is allowed to access from the secret store.
        /// </summary>
        /// <param name="storeName">Secret store name.</param>
        /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the secret store. The valid metadata keys and values are determined by the type of secret store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{T}" /> that will return the value when the operation has completed.</returns>
        public abstract Task<Dictionary<string, Dictionary<string, string>>> GetBulkSecretAsync(
            string storeName,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <inheritdoc />
        public void Dispose()
        {
            if (!this.disposed)
            {
                Dispose(disposing: true);
                this.disposed = true;
            }
        }

        /// <summary>
        /// Disposes the resources associated with the object.
        /// </summary>
        /// <param name="disposing"><c>true</c> if called by a call to the <c>Dispose</c> method; otherwise false.</param>
        protected virtual void Dispose(bool disposing)
        {
        }
    }
}
