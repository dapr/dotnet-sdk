// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
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
        /// and the path portion of the URI rewritten to follow the format of a Dapr service invocation request.
        /// </para>
        /// </summary>
        /// <param name="appId">
        /// An optional <c>app-id</c>. If specified, the <c>app-id</c> will be configured as the value of 
        /// <see cref="HttpClient.BaseAddress" /> so that relative URIs can be used. It is mandatory to set this parameter if your app-id contains at least one upper letter.
        /// If some requests use absolute URL with an app-id which contains at least one upper letter, it will not work, the workaround is to create one HttpClient for each app-id with the app-ip parameter set.
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
                DaprApiToken = daprApiToken,
                DefaultAppId = appId,
            };

            if (daprEndpoint is string)
            {
                // DaprEndpoint performs validation.
                handler.DaprEndpoint = daprEndpoint;
            }

            var httpClient = new HttpClient(handler);
            httpClient.DefaultRequestHeaders.UserAgent.Add(UserAgent());

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
        /// Optional token to be attached to all requests, defaults to <see cref="DaprDefaults.GetDefaultDaprApiToken"/>.
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
            return channel.Intercept(new InvocationInterceptor(appId, daprApiToken ?? DaprDefaults.GetDefaultDaprApiToken(null)));
        }

        internal static KeyValuePair<string, string>? GetDaprApiTokenHeader(string apiToken)
        {
            return string.IsNullOrWhiteSpace(apiToken)
                ? null
                : new KeyValuePair<string, string>("dapr-api-token", apiToken);
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
        /// // Bulk Publishes multiple events to the specified topic.
        /// </summary>
        /// <param name="pubsubName">The name of the pubsub component to use.</param>
        /// <param name="topicName">The name of the topic the request should be published to.</param>
        /// <param name="events">The list of events to be published.</param>
        /// <param name="metadata">The metadata to be set at the request level for the request.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.</returns>
        public abstract Task<BulkPublishResponse<TValue>> BulkPublishEventAsync<TValue>(
            string pubsubName,
            string topicName,
            IReadOnlyList<TValue> events,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Publishes an event to the specified topic.
        /// </summary>
        /// <param name="pubsubName">The name of the pubsub component to use.</param>
        /// <param name="topicName">The name of the topic the request should be published to.</param>
        /// <param name="data">The raw byte payload to inlcude in the message.</param>
        /// <param name="dataContentType">The content type of the given bytes, defaults to application/json.</param>
        /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the pubsub. The valid metadata keys and values are determined by the type of binding used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.</returns>
        public abstract Task PublishByteEventAsync(
            string pubsubName,
            string topicName,
            ReadOnlyMemory<byte> data,
            string dataContentType = Constants.ContentTypeApplicationJson,
            Dictionary<string, string> metadata = default,
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
        /// application identified by <paramref name="appId" /> and invokes the method specified by <paramref name="methodName" />
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
        /// application identified by <paramref name="appId" /> and invokes the method specified by <paramref name="methodName" />
        /// with the <c>POST</c> HTTP method.
        /// </summary>
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="queryStringParameters">A collection of key/value pairs to populate the query string from.</param>
        /// <returns>An <see cref="HttpRequestMessage" /> for use with <c>SendInvokeMethodRequestAsync</c>.</returns>
        public HttpRequestMessage CreateInvokeMethodRequest(string appId, string methodName, IReadOnlyCollection<KeyValuePair<string,string>> queryStringParameters)
        {
            return CreateInvokeMethodRequest(HttpMethod.Post, appId, methodName, queryStringParameters);
        }
        
        /// <summary>
        /// Creates an <see cref="HttpRequestMessage" /> that can be used to perform service invocation for the
        /// application identified by <paramref name="appId" /> and invokes the method specified by <paramref name="methodName" />
        /// with the HTTP method specified by <paramref name="httpMethod" />.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod" /> to use for the invocation request.</param>
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <returns>An <see cref="HttpRequestMessage" /> for use with <c>SendInvokeMethodRequestAsync</c>.</returns>
        public abstract HttpRequestMessage CreateInvokeMethodRequest(HttpMethod httpMethod, string appId, string methodName);

        /// <summary>
        /// Creates an <see cref="HttpRequestMessage" /> that can be used to perform service invocation for the
        /// application identified by <paramref name="appId" /> and invokes the method specified by <paramref name="methodName" />
        /// with the HTTP method specified by <paramref name="httpMethod" />.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod" /> to use for the invocation request.</param>
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="queryStringParameters">A collection of key/value pairs to populate the query string from.</param>
        /// <returns>An <see cref="HttpRequestMessage" /> for use with <c>SendInvokeMethodRequestAsync</c>.</returns>
        public abstract HttpRequestMessage CreateInvokeMethodRequest(HttpMethod httpMethod, string appId,
            string methodName, IReadOnlyCollection<KeyValuePair<string, string>> queryStringParameters);

        /// <summary>
        /// Creates an <see cref="HttpRequestMessage" /> that can be used to perform service invocation for the
        /// application identified by <paramref name="appId" /> and invokes the method specified by <paramref name="methodName" />
        /// with the <c>POST</c> HTTP method and a JSON serialized request body specified by <paramref name="data" />.
        /// </summary>
        /// <typeparam name="TRequest">The type of the data that will be JSON serialized and provided as the request body.</typeparam>
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="data">The data that will be JSON serialized and provided as the request body.</param>
        /// <returns>An <see cref="HttpRequestMessage" /> for use with <c>SendInvokeMethodRequestAsync</c>.</returns>
        public HttpRequestMessage CreateInvokeMethodRequest<TRequest>(string appId, string methodName, TRequest data)
        {
            return CreateInvokeMethodRequest(HttpMethod.Post, appId, methodName, new List<KeyValuePair<string, string>>(), data);
        }
        
        /// <summary>
        /// Creates an <see cref="HttpRequestMessage" /> that can be used to perform service invocation for the
        /// application identified by <paramref name="appId" /> and invokes the method specified by <paramref name="methodName" />
        /// with the HTTP method specified by <paramref name="httpMethod" /> and a JSON serialized request body specified by 
        /// <paramref name="data" />.
        /// </summary>
        /// <typeparam name="TRequest">The type of the data that will be JSON serialized and provided as the request body.</typeparam>
        /// <param name="httpMethod">The <see cref="HttpMethod" /> to use for the invocation request.</param>
        /// <param name="appId">The Dapr application id to invoke the method on.</param>
        /// <param name="methodName">The name of the method to invoke.</param>
        /// <param name="data">The data that will be JSON serialized and provided as the request body.</param>
        /// <param name="queryStringParameters">A collection of key/value pairs to populate the query string from.</param>
        /// <returns>An <see cref="HttpRequestMessage" /> for use with <c>SendInvokeMethodRequestAsync</c>.</returns>
        public abstract HttpRequestMessage CreateInvokeMethodRequest<TRequest>(HttpMethod httpMethod, string appId, string methodName, IReadOnlyCollection<KeyValuePair<string,string>> queryStringParameters, TRequest data);
        
        /// <summary>
        /// Perform health-check of Dapr sidecar. Return 'true' if sidecar is healthy. Otherwise 'false'.
        /// CheckHealthAsync handle <see cref="HttpRequestException"/> and will return 'false' if error will occur on transport level
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{T}" /> that will return the value when the operation has completed.</returns>
        public abstract Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Perform health-check of Dapr sidecar's outbound APIs. Return 'true' if the sidecar is healthy. Otherwise false. This method should
        /// be used over <see cref="CheckHealthAsync(CancellationToken)"/> when the health of Dapr is being checked before it starts. This
        /// health endpoint indicates that Dapr has stood up its APIs and is currently waiting on this application to report fully healthy.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{T}" /> that will return the value when the operation has completed.</returns>
        public abstract Task<bool> CheckOutboundHealthAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Calls <see cref="CheckOutboundHealthAsync(CancellationToken)"/> until the sidecar is reporting as healthy. If the sidecar
        /// does not become healthy, an exception will be thrown.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will return when the operation has completed.</returns>
        public abstract Task WaitForSidecarAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Send a command to the Dapr Sidecar telling it to shutdown.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will return when the operation has completed.</returns>
        public abstract Task ShutdownSidecarAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Calls the sidecar's metadata endpoint which returns information including:
        /// <list type="bullet">
        /// <item>
        /// <description>The sidecar's ID.</description>
        /// </item>
        /// <item>
        /// <description>The registered/active actors if any.</description>
        /// </item>
        /// <item>
        /// <description>Registered components including name, type, version, and information on capabilities if present.</description>
        /// </item>
        /// <item>
        /// <description>Any extended metadata that has been set via <see cref="SetMetadataAsync"/></description>
        /// </item>
        /// </list>
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{T}" /> that will return the value when the operation has completed.</returns>
        public abstract Task<DaprMetadata> GetMetadataAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Perform service add extended metadata to the Dapr sidecar.
        /// </summary>
        /// <param name="attributeName">Custom attribute name</param>
        /// <param name="attributeValue">Custom attribute value</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will return the value when the operation has completed.</returns>
        public abstract Task SetMetadataAsync(string attributeName, string attributeValue, CancellationToken cancellationToken = default);

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

#nullable enable
        /// <summary>
        /// <para>
        /// Creates an <see cref="HttpClient"/> that can be used to perform Dapr service invocation using <see cref="HttpRequestMessage"/>
        /// objects.
        /// </para>
        /// <para>
        /// The client will read the <see cref="HttpRequestMessage.RequestUri" /> property, and 
        /// interpret the hostname as the destination <c>app-id</c>. The <see cref="HttpRequestMessage.RequestUri" /> 
        /// property will be replaced with a new URI with the authority section replaced by the HTTP endpoint value
        /// and the path portion of the URI rewritten to follow the format of a Dapr service invocation request.
        /// </para>
        /// </summary>
        /// <param name="appId">
        ///     An optional <c>app-id</c>. If specified, the <c>app-id</c> will be configured as the value of 
        ///     <see cref="HttpClient.BaseAddress" /> so that relative URIs can be used. It is mandatory to set this parameter if your app-id contains at least one upper letter.
        ///     If some requests use absolute URL with an app-id which contains at least one upper letter, it will not work, the workaround is to create one HttpClient for each app-id with the app-ip parameter set.
        /// </param>
        /// <returns>An <see cref="HttpClient" /> that can be used to perform service invocation requests.</returns>
        /// <remarks>
        /// </remarks>
        public abstract HttpClient CreateInvokableHttpClient(string? appId = null);
#nullable disable

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
        /// Perform service invocation for the application identified by <paramref name="appId" /> and invokes the method 
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
        /// Perform service invocation for the application identified by <paramref name="appId" /> and invokes the method 
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
        /// Perform service invocation for the application identified by <paramref name="appId" /> and invokes the method 
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
        /// Perform service invocation for the application identified by <paramref name="appId" /> and invokes the method 
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
            var request = CreateInvokeMethodRequest<TRequest>(httpMethod, appId, methodName, new List<KeyValuePair<string, string>>(), data);
            return InvokeMethodAsync(request, cancellationToken);
        }

        /// <summary>
        /// Perform service invocation for the application identified by <paramref name="appId" /> and invokes the method 
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
        /// Perform service invocation for the application identified by <paramref name="appId" /> and invokes the method 
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
        /// Perform service invocation for the application identified by <paramref name="appId" /> and invokes the method 
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
        /// Perform service invocation for the application identified by <paramref name="appId" /> and invokes the method 
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
            var request = CreateInvokeMethodRequest<TRequest>(httpMethod, appId, methodName, new List<KeyValuePair<string, string>>(), data);
            return InvokeMethodAsync<TResponse>(request, cancellationToken);
        }

        /// <summary>
        /// Perform service invocation using gRPC semantics for the application identified by <paramref name="appId" /> and invokes the method 
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
        /// Perform service invocation using gRPC semantics for the application identified by <paramref name="appId" /> and invokes the method 
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
        /// Perform service invocation using gRPC semantics for the application identified by <paramref name="appId" /> and invokes the method 
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
        /// Perform service invocation using gRPC semantics for the application identified by <paramref name="appId" /> and invokes the method 
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
        /// Gets a list of deserialized values associated with the <paramref name="keys" /> from the Dapr state store. This overload should be used
        /// if you expect the values of all the retrieved items to match the shape of the indicated <typeparam name="TValue"/>. If you expect that
        /// the values may differ in type from one another, do not specify the type parameter and instead use the original <see cref="GetBulkStateAsync"/> method
        /// so the serialized string values will be returned instead.
        /// </summary>
        /// <param name="storeName">The name of state store to read from.</param>
        /// <param name="keys">The list of keys to get values for.</param>
        /// <param name="parallelism">The number of concurrent get operations the Dapr runtime will issue to the state store. a value equal to or smaller than 0 means max parallelism.</param>
        /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the state store. The valid metadata keys and values are determined by the type of state store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{IReadOnlyList}" /> that will return the list of deserialized values when the operation has completed.</returns>
        public abstract Task<IReadOnlyList<BulkStateItem<TValue>>> GetBulkStateAsync<TValue>(string storeName, IReadOnlyList<string> keys, int? parallelism, IReadOnlyDictionary<string, string> metadata = default, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Saves a list of <paramref name="items" /> to the Dapr state store.
        /// </summary>
        /// <param name="storeName">The name of state store.</param>
        /// <param name="items">The list of items to save.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.</returns>
        public abstract Task SaveBulkStateAsync<TValue>(string storeName, IReadOnlyList<SaveStateItem<TValue>> items, CancellationToken cancellationToken = default);

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
        /// Saves the provided <paramref name="binaryValue" /> associated with the provided <paramref name="key" /> to the Dapr state
        /// store
        /// </summary>
        /// <param name="storeName">The name of the state store.</param>
        /// <param name="key">The state key.</param>
        /// <param name="binaryValue">The binary data that will be stored in the state store.</param>        
        /// <param name="stateOptions">Options for performing save state operation.</param>
        /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the state store. The valid metadata keys and values are determined by the type of state store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.</returns>
        public abstract Task SaveByteStateAsync(
                       string storeName,
                       string key,
                       ReadOnlyMemory<byte> binaryValue,
                       StateOptions stateOptions = default,
                       IReadOnlyDictionary<string, string> metadata = default,
                       CancellationToken cancellationToken = default);

        /// <summary>
        ///Saves the provided <paramref name="binaryValue" /> associated with the provided <paramref name="key" /> using the 
        /// <paramref name="etag"/> to the Dapr state. State store implementation will allow the update only if the attached ETag matches with the latest ETag in the state store.
        /// </summary>
        /// <param name="storeName">The name of the state store.</param>
        /// <param name="key">The state key.</param>
        /// <param name="binaryValue">The binary data that will be stored in the state store.</param> 
        /// <param name="etag">An ETag.</param>
        /// <param name="stateOptions">Options for performing save state operation.</param>
        /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the state store. The valid metadata keys and values are determined by the type of state store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task" /> that will complete when the operation has completed.</returns>
        public abstract Task<bool> TrySaveByteStateAsync(
                string storeName,
                string key,
                ReadOnlyMemory<byte> binaryValue,
                string etag,
                StateOptions stateOptions = default,
                IReadOnlyDictionary<string, string> metadata = default,
                CancellationToken cancellationToken = default);


        /// <summary>
        /// Gets the current binary value associated with the <paramref name="key" /> from the Dapr state store.
        /// </summary>
        /// <param name="storeName">The name of state store to read from.</param>
        /// <param name="key">The state key.</param>
        /// <param name="consistencyMode">The consistency mode <see cref="ConsistencyMode" />.</param>
        /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the state store. The valid metadata keys and values are determined by the type of state store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{T}" /> that will return the value when the operation has completed.</returns>
        public abstract Task<ReadOnlyMemory<byte>> GetByteStateAsync(
                string storeName,
                string key,
                ConsistencyMode? consistencyMode = default,
                IReadOnlyDictionary<string, string> metadata = default,
                CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current binary value associated with the <paramref name="key" /> from the Dapr state store and an ETag.
        /// </summary>
        /// <param name="storeName">The name of the state store.</param>
        /// <param name="key">The state key.</param>
        /// <param name="consistencyMode">The consistency mode <see cref="ConsistencyMode" />.</param>
        /// <param name="metadata">A collection of metadata key-value pairs that will be provided to the state store. The valid metadata keys and values are determined by the type of state store used.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task{T}" /> that will return the value when the operation has completed.  This wraps the read value and an ETag.</returns>
        public abstract Task<(ReadOnlyMemory<byte>, string etag)> GetByteStateAndETagAsync(
            string storeName, 
            string key, 
            ConsistencyMode? consistencyMode = default, 
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
        /// Queries the specified statestore with the given query. The query is a JSON representation of the query as described by the Dapr QueryState API.
        /// Note that the underlying statestore must support queries.
        /// </summary>
        /// <param name="storeName">The name of the statestore.</param>
        /// <param name="jsonQuery">A JSON formatted query string.</param>
        /// <param name="metadata">Metadata to send to the statestore.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The data type of the value to read.</typeparam>
        /// <returns>A <see cref="StateQueryResponse{TValue}"/> that may be paginated, use <see cref="StateQueryResponse{TValue}.Token"/> to continue the query.</returns>
        public abstract Task<StateQueryResponse<TValue>> QueryStateAsync<TValue>(
            string storeName,
            string jsonQuery,
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

        /// <summary>
        /// Get a list of configuration items based on keys from the given statestore. 
        /// </summary>
        /// <param name="storeName">The name of the configuration store to be queried.</param>
        /// <param name="keys">An optional list of keys to query for. If provided, the result will only contain those keys. An empty list indicates all keys should be fetched.</param>
        /// <param name="metadata">Optional metadata that will be sent to the configuration store being queried.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="GetConfigurationResponse"/></returns>
        public abstract Task<GetConfigurationResponse> GetConfiguration(
            string storeName,
            IReadOnlyList<string> keys,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Subscribe to a configuration store for the specified keys and receive an updated value whenever the key is updated in the store.
        /// </summary>
        /// <param name="storeName">The name of the configuration store to be queried.</param>
        /// <param name="keys">An optional list of keys to query for. If provided, the result will only contain those keys. An empty list indicates all keys should be fetched.</param>
        /// <param name="metadata">Optional metadata that will be sent to the configuration store being queried.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="SubscribeConfigurationResponse"/> which contains a reference to the stream.</returns>
        public abstract Task<SubscribeConfigurationResponse> SubscribeConfiguration(
            string storeName,
            IReadOnlyList<string> keys,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Unsubscribe from a configuration store using the specified Id.
        /// </summary>
        /// <param name="storeName">The name of the configuration store.</param>
        /// <param name="id">The Id of the subscription that should no longer be watched.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns></returns>
        public abstract Task<UnsubscribeConfigurationResponse> UnsubscribeConfiguration(
            string storeName,
            string id,
            CancellationToken cancellationToken = default);

        #region Cryptography

        /// <summary>
        /// Encrypts an array of bytes using the Dapr Cryptography encryption functionality.
        /// </summary>
        /// <param name="vaultResourceName">The name of the vault resource used by the operation.</param>
        /// <param name="plaintextBytes">The bytes of the plaintext value to encrypt.</param>
        /// <param name="keyName">The name of the key to use from the Vault for the encryption operation.</param>
        /// <param name="encryptionOptions">Options informing how the encryption operation should be configured.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
        /// <returns>An array of encrypted bytes.</returns>
        [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
        public abstract Task<ReadOnlyMemory<byte>> EncryptAsync(string vaultResourceName,
            ReadOnlyMemory<byte> plaintextBytes, string keyName, EncryptionOptions encryptionOptions,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Encrypts a stream using the Dapr Cryptography encryption functionality.
        /// </summary>
        /// <param name="vaultResourceName">The name of the vault resource used by the operation.</param>
        /// <param name="plaintextStream">The stream containing the bytes of the plaintext value to encrypt.</param>
        /// <param name="keyName">The name of the key to use from the Vault for the encryption operation.</param>
        /// <param name="encryptionOptions">Options informing how the encryption operation should be configured.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
        /// <returns>An array of encrypted bytes.</returns>
        [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
        public abstract IAsyncEnumerable<ReadOnlyMemory<byte>> EncryptAsync(string vaultResourceName, Stream plaintextStream, string keyName,
            EncryptionOptions encryptionOptions, CancellationToken cancellationToken = default);

        /// <summary>
        /// Decrypts the specified ciphertext bytes using the Dapr Cryptography encryption functionality.
        /// </summary>
        /// <param name="vaultResourceName">The name of the vault resource used by the operation.</param>
        /// <param name="ciphertextBytes">The bytes of the ciphertext value to decrypt.</param>
        /// <param name="keyName">The name of the key to use from the Vault for the decryption operation.</param>
        /// <param name="options">Options informing how the decryption operation should be configured.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
        /// <returns>An array of decrypted bytes.</returns>
        [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
        public abstract Task<ReadOnlyMemory<byte>> DecryptAsync(string vaultResourceName, ReadOnlyMemory<byte> ciphertextBytes, string keyName, DecryptionOptions options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Decrypts the specified ciphertext bytes using the Dapr Cryptography encryption functionality.
        /// </summary>
        /// <param name="vaultResourceName">The name of the vault resource used by the operation.</param>
        /// <param name="ciphertextBytes">The bytes of the ciphertext value to decrypt.</param>
        /// <param name="keyName">The name of the key to use from the Vault for the decryption operation.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
        /// <returns>An array of decrypted bytes.</returns>
        [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
        public abstract Task<ReadOnlyMemory<byte>> DecryptAsync(string vaultResourceName,
            ReadOnlyMemory<byte> ciphertextBytes, string keyName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Decrypts the specified stream of ciphertext using the Dapr Cryptography encryption functionality.
        /// </summary>
        /// <param name="vaultResourceName">The name of the vault resource used by the operation.</param>
        /// <param name="ciphertextStream">The stream containing the bytes of the ciphertext value to decrypt.</param>
        /// <param name="keyName">The name of the key to use from the Vault for the decryption operation.</param>
        /// <param name="options">Options informing how the decryption operation should be configured.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
        /// <returns>An asynchronously enumerable array of decrypted bytes.</returns>
        [Obsolete(
            "The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
        public abstract IAsyncEnumerable<ReadOnlyMemory<byte>> DecryptAsync(string vaultResourceName, Stream ciphertextStream,
            string keyName, DecryptionOptions options, CancellationToken cancellationToken = default);

        /// <summary>
        /// Decrypts the specified stream of ciphertext using the Dapr Cryptography encryption functionality.
        /// </summary>
        /// <param name="vaultResourceName">The name of the vault resource used by the operation.</param>
        /// <param name="ciphertextStream">The stream containing the bytes of the ciphertext value to decrypt.</param>
        /// <param name="keyName">The name of the key to use from the Vault for the decryption operation.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
        /// <returns>An asynchronously enumerable array of decrypted bytes.</returns>
        [Obsolete(
            "The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
        public abstract IAsyncEnumerable<ReadOnlyMemory<byte>> DecryptAsync(string vaultResourceName, Stream ciphertextStream,
            string keyName, CancellationToken cancellationToken = default);

        #endregion

        #region Cryptography - Subtle API

        ///// <summary>
        ///// Retrieves the value of the specified key from the vault.
        ///// </summary>
        ///// <param name="vaultResourceName">The name of the vault resource used by the operation.</param>
        ///// <param name="keyName">The name of the key to retrieve the value of.</param>
        ///// <param name="keyFormat">The format to use for the key result.</param>
        ///// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
        ///// <returns>The name (and possibly version as name/version) of the key and its public key.</returns>
        //[Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
        //public abstract Task<(string Name, string PublicKey)> GetKeyAsync(string vaultResourceName, string keyName, SubtleGetKeyRequest.Types.KeyFormat keyFormat,
        //    CancellationToken cancellationToken = default);

        ///// <summary>
        ///// Encrypts an array of bytes using the Dapr Cryptography functionality.
        ///// </summary>
        ///// <param name="vaultResourceName">The name of the vault resource used by the operation.</param>
        ///// <param name="plainTextBytes">The bytes of the plaintext value to encrypt.</param>
        ///// <param name="algorithm">The name of the algorithm that should be used to perform the encryption.</param>
        ///// <param name="keyName">The name of the key used to perform the encryption operation.</param>
        ///// <param name="nonce">The bytes comprising the nonce.</param>
        ///// <param name="associatedData">Any associated data when using AEAD ciphers.</param>
        ///// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
        ///// <returns>The array of encrypted bytes.</returns>
        //[Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
        //public abstract Task<(byte[] CipherTextBytes, byte[] AuthenticationTag)> EncryptAsync(
        //    string vaultResourceName,
        //    byte[] plainTextBytes,
        //    string algorithm,
        //    string keyName,
        //    byte[] nonce,
        //    byte[] associatedData,
        //    CancellationToken cancellationToken = default);

        ///// <summary>
        ///// Encrypts an array of bytes using the Dapr Cryptography functionality.
        ///// </summary>
        ///// <param name="vaultResourceName">The name of the vault resource used by the operation.</param>
        ///// <param name="plainTextBytes">The bytes of the plaintext value to encrypt.</param>
        ///// <param name="algorithm">The name of the algorithm that should be used to perform the encryption.</param>
        ///// <param name="keyName">The name of the key used to perform the encryption operation.</param>
        ///// <param name="nonce">The bytes comprising the nonce.</param>
        ///// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
        ///// <returns>The array of encrypted bytes.</returns>
        //[Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
        //public async Task<(byte[] CipherTextBytes, byte[] AuthenticationTag)> EncryptAsync(
        //    string vaultResourceName,
        //    byte[] plainTextBytes,
        //    string algorithm,
        //    string keyName,
        //    byte[] nonce,
        //    CancellationToken cancellationToken = default) =>
        //    await EncryptAsync(vaultResourceName, plainTextBytes, algorithm, keyName, nonce, Array.Empty<byte>(),
        //        cancellationToken);

        ///// <summary>
        ///// Decrypts an array of bytes using the Dapr Cryptography functionality.
        ///// </summary>
        ///// <param name="vaultResourceName">The name of the vault the key is retrieved from for the decryption operation.</param>
        ///// <param name="cipherTextBytes">The array of bytes to decrypt.</param>
        ///// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
        ///// <param name="algorithm">The algorithm to use to perform the decryption operation.</param>
        ///// <param name="keyName">The name of the key used for the decryption.</param>
        ///// <param name="nonce">The nonce value used.</param>
        ///// <param name="tag"></param>
        ///// <param name="associatedData">Any associated data when using AEAD ciphers.</param>
        ///// <returns>The array of plaintext bytes.</returns>
        //[Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
        //public abstract Task<byte[]> DecryptAsync(string vaultResourceName, byte[] cipherTextBytes,
        //    string algorithm, string keyName, byte[] nonce, byte[] tag, byte[] associatedData,
        //    CancellationToken cancellationToken = default);

        ///// <summary>
        ///// Decrypts an array of bytes using the Dapr Cryptography functionality.
        ///// </summary>
        ///// <param name="vaultResourceName">The name of the vault the key is retrieved from for the decryption operation.</param>
        ///// <param name="cipherTextBytes">The array of bytes to decrypt.</param>
        ///// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
        ///// <param name="algorithm">The algorithm to use to perform the decryption operation.</param>
        ///// <param name="keyName">The name of the key used for the decryption.</param>
        ///// <param name="nonce">The nonce value used.</param>
        ///// <param name="tag"></param>
        ///// <returns>The array of plaintext bytes.</returns>
        //[Obsolete(
        //    "The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
        //public async Task<byte[]> DecryptAsync(string vaultResourceName, byte[] cipherTextBytes,
        //    string algorithm, string keyName, byte[] nonce, byte[] tag, CancellationToken cancellationToken = default) =>
        //    await DecryptAsync(vaultResourceName, cipherTextBytes, algorithm, keyName, nonce, tag, Array.Empty<byte>(), cancellationToken);

        ///// <summary>
        ///// Wraps the plaintext key using another.
        ///// </summary>
        ///// <param name="vaultResourceName">The name of the vault to retrieve the key from.</param>
        ///// <param name="plainTextKey">The plaintext bytes comprising the key to wrap.</param>
        ///// <param name="keyName">The name of the key used to wrap the <paramref name="plainTextKey"/> value.</param>
        ///// <param name="algorithm">The algorithm to use to perform the wrap operation.</param>
        ///// <param name="nonce">The none used.</param>
        ///// <param name="associatedData">Any associated data when using AEAD ciphers.</param>
        ///// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
        ///// <returns>The bytes comprising the wrapped plain-text key and the authentication tag, if applicable.</returns>
        //[Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
        //public abstract Task<(byte[] WrappedKey, byte[] AuthenticationTag)> WrapKeyAsync(string vaultResourceName, byte[] plainTextKey, string keyName, string algorithm, byte[] nonce, byte[] associatedData,
        //    CancellationToken cancellationToken = default);

        ///// <summary>
        ///// Wraps the plaintext key using another.
        ///// </summary>
        ///// <param name="vaultResourceName">The name of the vault to retrieve the key from.</param>
        ///// <param name="plainTextKey">The plaintext bytes comprising the key to wrap.</param>
        ///// <param name="keyName">The name of the key used to wrap the <paramref name="plainTextKey"/> value.</param>
        ///// <param name="algorithm">The algorithm to use to perform the wrap operation.</param>
        ///// <param name="nonce">The none used.</param>
        ///// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
        ///// <returns>The bytes comprising the unwrapped key and the authentication tag, if applicable.</returns>
        //[Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
        //public async Task<(byte[] WrappedKey, byte[] AuthenticationTag)> WrapKeyAsync(string vaultResourceName, byte[] plainTextKey, string keyName, string algorithm,
        //    byte[] nonce, CancellationToken cancellationToken = default) => await WrapKeyAsync(vaultResourceName, plainTextKey,
        //    keyName, algorithm, nonce, Array.Empty<byte>(), cancellationToken);

        ///// <summary>
        ///// Used to unwrap the specified key.
        ///// </summary>
        ///// <param name="vaultResourceName">The name of the vault to retrieve the key from.</param>
        ///// <param name="wrappedKey">The byte comprising the wrapped key.</param>
        ///// <param name="algorithm">The algorithm to use in unwrapping the key.</param>
        ///// <param name="keyName">The name of the key used to unwrap the wrapped key bytes.</param>
        ///// <param name="nonce">The nonce value.</param>
        ///// <param name="tag">The bytes comprising the authentication tag.</param>
        ///// <param name="associatedData">Any associated data when using AEAD ciphers.</param>
        ///// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
        ///// <returns>The bytes comprising the unwrapped key.</returns>
        //[Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
        //public abstract Task<byte[]> UnwrapKeyAsync(string vaultResourceName, byte[] wrappedKey, string algorithm, string keyName, byte[] nonce, byte[] tag, byte[] associatedData,
        //    CancellationToken cancellationToken = default);

        ///// <summary>
        ///// Used to unwrap the specified key.
        ///// </summary>
        ///// <param name="vaultResourceName">The name of the vault to retrieve the key from.</param>
        ///// <param name="wrappedKey">The byte comprising the wrapped key.</param>
        ///// <param name="algorithm">The algorithm to use in unwrapping the key.</param>
        ///// <param name="keyName">The name of the key used to unwrap the wrapped key bytes.</param>
        ///// <param name="nonce">The nonce value.</param>
        ///// <param name="tag">The bytes comprising the authentication tag.</param>
        ///// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
        ///// <returns>The bytes comprising the unwrapped key.</returns>
        //[Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
        //public async Task<byte[]> UnwrapKeyAsync(string vaultResourceName, byte[] wrappedKey, string algorithm, string keyName,
        //    byte[] nonce, byte[] tag,
        //    CancellationToken cancellationToken = default) => await UnwrapKeyAsync(vaultResourceName,
        //    wrappedKey, algorithm, keyName, nonce, Array.Empty<byte>(), Array.Empty<byte>(), cancellationToken);

        ///// <summary>
        ///// Used to unwrap the specified key.
        ///// </summary>
        ///// <param name="vaultResourceName">The name of the vault to retrieve the key from.</param>
        ///// <param name="wrappedKey">The byte comprising the wrapped key.</param>
        ///// <param name="algorithm">The algorithm to use in unwrapping the key.</param>
        ///// <param name="keyName">The name of the key used to unwrap the wrapped key bytes.</param>
        ///// <param name="nonce">The nonce value.</param>
        ///// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
        ///// <returns>The bytes comprising the unwrapped key.</returns>
        //[Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
        //public async Task<byte[]> UnwrapKeyAsync(string vaultResourceName, byte[] wrappedKey, string algorithm, string keyName,
        //    byte[] nonce, CancellationToken cancellationToken = default) => await UnwrapKeyAsync(vaultResourceName,
        //    wrappedKey, algorithm, keyName, nonce, Array.Empty<byte>(), Array.Empty<byte>(), cancellationToken);

        ///// <summary>
        ///// Creates a signature of a digest value.
        ///// </summary>
        ///// <param name="vaultResourceName">The name of the vault to retrieve the key from.</param>
        ///// <param name="digest">The digest value to create the signature for.</param>
        ///// <param name="algorithm">The algorithm used to create the signature.</param>
        ///// <param name="keyName">The name of the key used.</param>
        ///// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
        ///// <returns>The bytes comprising the signature.</returns>
        //[Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
        //public abstract Task<byte[]> SignAsync(string vaultResourceName, byte[] digest, string algorithm, string keyName,
        //    CancellationToken cancellationToken = default);

        ///// <summary>
        ///// Validates a signature.
        ///// </summary>
        ///// <param name="vaultResourceName">The name of the vault to retrieve the key from.</param>
        ///// <param name="digest">The digest to validate the signature with.</param>
        ///// <param name="signature">The signature to validate.</param>
        ///// <param name="algorithm">The algorithm to validate the signature with.</param>
        ///// <param name="keyName">The name of the key used.</param>
        ///// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
        ///// <returns><c>True</c> if the signature verification is successful; otherwise <c>false</c>.</returns>
        //[Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
        //public abstract Task<bool> VerifyAsync(string vaultResourceName, byte[] digest, byte[] signature, string algorithm, string keyName,
        //    CancellationToken cancellationToken = default);

        #endregion

        /// <summary>
        /// Attempt to lock the given resourceId with response indicating success.
        /// </summary>
        /// <param name="storeName">The name of the lock store to be queried.</param>
        /// <param name="resourceId">Lock key that stands for which resource to protect.</param>
        /// <param name="lockOwner">Indicates the identifier of lock owner.</param>
        /// <param name="expiryInSeconds">The time after which the lock gets expired.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="TryLockResponse"/></returns>
        [Obsolete("This API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
        public abstract Task<TryLockResponse> Lock(
            string storeName,
            string resourceId,
            string lockOwner,
            Int32 expiryInSeconds,
            CancellationToken cancellationToken = default);


        /// <summary>
        /// Attempt to unlock the given resourceId with response indicating success. 
        /// </summary>
        /// <param name="storeName">The name of the lock store to be queried.</param>
        /// <param name="resourceId">Lock key that stands for which resource to protect.</param>
        /// <param name="lockOwner">Indicates the identifier of lock owner.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="Task"/> containing a <see cref="UnlockResponse"/></returns>
        [Obsolete("This API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
        public abstract Task<UnlockResponse> Unlock(
            string storeName,
            string resourceId,
            string lockOwner,
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

        /// <summary>
        /// Returns the value for the User-Agent.
        /// </summary>
        /// <returns>A <see cref="ProductInfoHeaderValue"/> containing the value to use for User-Agent.</returns>
        protected static ProductInfoHeaderValue UserAgent()
        {
            var assembly = typeof(DaprClient).Assembly;
            string assemblyVersion = assembly
                .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
                .FirstOrDefault()?
                .InformationalVersion;

            return new ProductInfoHeaderValue("dapr-sdk-dotnet", $"v{assemblyVersion}");
        }
    }
}
