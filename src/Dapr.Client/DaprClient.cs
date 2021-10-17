// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Google.Protobuf;
    using Grpc.Core;

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
    public abstract class DaprClient : IDisposable, IDaprClient
    {
        private bool disposed;

        /// <inheritdoc/>
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

        internal static KeyValuePair<string, string>? GetDaprApiTokenHeader(string apiToken)
        {
            if (string.IsNullOrWhiteSpace(apiToken))
            {
                return null;
            }

            return new KeyValuePair<string, string>("dapr-api-token", apiToken);
        }

        /// <inheritdoc/>
        public abstract Task PublishEventAsync<TData>(
            string pubsubName,
            string topicName,
            TData data,
            CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task PublishEventAsync<TData>(
            string pubsubName,
            string topicName,
            TData data,
            Dictionary<string, string> metadata,
            CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task PublishEventAsync(
            string pubsubName,
            string topicName,
            CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task PublishEventAsync(
            string pubsubName,
            string topicName,
            Dictionary<string, string> metadata,
            CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task InvokeBindingAsync<TRequest>(
            string bindingName,
            string operation,
            TRequest data,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task<TResponse> InvokeBindingAsync<TRequest, TResponse>(
            string bindingName,
            string operation,
            TRequest data,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task<BindingResponse> InvokeBindingAsync(
            BindingRequest request,
            CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public HttpRequestMessage CreateInvokeMethodRequest(string appId, string methodName)
        {
            return CreateInvokeMethodRequest(HttpMethod.Post, appId, methodName);
        }

        /// <inheritdoc/>
        public abstract HttpRequestMessage CreateInvokeMethodRequest(HttpMethod httpMethod, string appId, string methodName);

        /// <inheritdoc/>
        public HttpRequestMessage CreateInvokeMethodRequest<TRequest>(string appId, string methodName, TRequest data)
        {
            return CreateInvokeMethodRequest<TRequest>(HttpMethod.Post, appId, methodName, data);
        }

        /// <inheritdoc/>
        public abstract HttpRequestMessage CreateInvokeMethodRequest<TRequest>(HttpMethod httpMethod, string appId, string methodName, TRequest data);

        /// <inheritdoc/>
        public abstract Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task<HttpResponseMessage> InvokeMethodWithResponseAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task InvokeMethodAsync(HttpRequestMessage request, CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task<TResponse> InvokeMethodAsync<TResponse>(HttpRequestMessage request, CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public Task InvokeMethodAsync(
            string appId,
            string methodName,
            CancellationToken cancellationToken = default)
        {
            var request = CreateInvokeMethodRequest(appId, methodName);
            return InvokeMethodAsync(request, cancellationToken);
        }

        /// <inheritdoc/>
        public Task InvokeMethodAsync(
            HttpMethod httpMethod,
            string appId,
            string methodName,
            CancellationToken cancellationToken = default)
        {
            var request = CreateInvokeMethodRequest(httpMethod, appId, methodName);
            return InvokeMethodAsync(request, cancellationToken);
        }

        /// <inheritdoc/>
        public Task InvokeMethodAsync<TRequest>(
            string appId,
            string methodName,
            TRequest data,
            CancellationToken cancellationToken = default)
        {
            var request = CreateInvokeMethodRequest<TRequest>(appId, methodName, data);
            return InvokeMethodAsync(request, cancellationToken);
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public Task<TResponse> InvokeMethodAsync<TResponse>(
            string appId,
            string methodName,
            CancellationToken cancellationToken = default)
        {
            var request = CreateInvokeMethodRequest(appId, methodName);
            return InvokeMethodAsync<TResponse>(request, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<TResponse> InvokeMethodAsync<TResponse>(
            HttpMethod httpMethod,
            string appId,
            string methodName,
            CancellationToken cancellationToken = default)
        {
            var request = CreateInvokeMethodRequest(httpMethod, appId, methodName);
            return InvokeMethodAsync<TResponse>(request, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<TResponse> InvokeMethodAsync<TRequest, TResponse>(
            string appId,
            string methodName,
            TRequest data,
            CancellationToken cancellationToken = default)
        {
            var request = CreateInvokeMethodRequest<TRequest>(appId, methodName, data);
            return InvokeMethodAsync<TResponse>(request, cancellationToken);
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public abstract Task InvokeMethodGrpcAsync(
            string appId,
            string methodName,
            CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task InvokeMethodGrpcAsync<TRequest>(
            string appId,
            string methodName,
            TRequest data,
            CancellationToken cancellationToken = default)
        where TRequest : IMessage;

        /// <inheritdoc/>
        public abstract Task<TResponse> InvokeMethodGrpcAsync<TResponse>(
            string appId,
            string methodName,
            CancellationToken cancellationToken = default)
        where TResponse : IMessage, new();

        /// <inheritdoc/>
        public abstract Task<TResponse> InvokeMethodGrpcAsync<TRequest, TResponse>(
            string appId,
            string methodName,
            TRequest data,
            CancellationToken cancellationToken = default)
        where TRequest : IMessage
        where TResponse : IMessage, new();

        /// <inheritdoc/>
        public abstract Task<TValue> GetStateAsync<TValue>(string storeName, string key, ConsistencyMode? consistencyMode = default, IReadOnlyDictionary<string, string> metadata = default, CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task<IReadOnlyList<BulkStateItem>> GetBulkStateAsync(string storeName, IReadOnlyList<string> keys, int? parallelism, IReadOnlyDictionary<string, string> metadata = default, CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task DeleteBulkStateAsync(string storeName, IReadOnlyList<BulkDeleteStateItem> items, CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task<(TValue value, string etag)> GetStateAndETagAsync<TValue>(string storeName, string key, ConsistencyMode? consistencyMode = default, IReadOnlyDictionary<string, string> metadata = default, CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public async Task<StateEntry<TValue>> GetStateEntryAsync<TValue>(string storeName, string key, ConsistencyMode? consistencyMode = default, IReadOnlyDictionary<string, string> metadata = default, CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

            var (state, etag) = await this.GetStateAndETagAsync<TValue>(storeName, key, consistencyMode, metadata, cancellationToken);
            return new StateEntry<TValue>(this, storeName, key, state, etag);
        }

        /// <inheritdoc/>
        public abstract Task SaveStateAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            StateOptions stateOptions = default,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task<bool> TrySaveStateAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            string etag,
            StateOptions stateOptions = default,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task ExecuteStateTransactionAsync(
            string storeName,
            IReadOnlyList<StateTransactionRequest> operations,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task DeleteStateAsync(
            string storeName,
            string key,
            StateOptions stateOptions = default,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task<bool> TryDeleteStateAsync(
            string storeName,
            string key,
            string etag,
            StateOptions stateOptions = default,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task<Dictionary<string, string>> GetSecretAsync(
            string storeName,
            string key,
            IReadOnlyDictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default);

        /// <inheritdoc/>
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
