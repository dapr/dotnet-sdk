// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------
namespace Dapr
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using static Dapr.DaprUris;

    /// <summary>
    /// A client for interacting with the Dapr invoke endpoints using <see cref="HttpClient" />.
    /// </summary>
    public class InvokeHttpClient : InvokeClient
    {
        private const string DaprDefaultEndpoint = "127.0.0.1";
        private readonly HttpClient client;
        private readonly JsonSerializerOptions serializerOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="InvokeHttpClient"/> class.
        /// </summary>
        /// <param name="client">The <see cref="HttpClient" />.</param>
        /// <param name="serializerOptions">The <see cref="JsonSerializerOptions" />.</param>
        public InvokeHttpClient(HttpClient client, JsonSerializerOptions serializerOptions = null)
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            this.client = client;
            this.serializerOptions = serializerOptions;
        }

        /// <summary>
        /// Invokes the specified method on target service, Request and Response data is serialized/deserialized using the default System.Text.Json Serialization.
        /// </summary>
        /// <param name="serviceName">The name of the service to be called in the Dapr invoke request.</param>
        /// <param name="methodName">THe name of the method to be called in the Dapr invoke request.</param>
        /// <param name="data">The data to be sent within the body of the Dapr invoke request.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TRequest">The data type of the object that will be serialized.</typeparam>
        /// <typeparam name="TResponse">The data type that the Dapr response body will be deserialized to.</typeparam>
        /// <returns>A <see cref="Task" /> that will return a deserialized object of the reponse from the Invoke request. If the Dapr response content is null the return type will be null.</returns>
        public override async Task<TResponse> InvokeMethodAsync<TRequest, TResponse>(string serviceName, string methodName, TRequest data, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(serviceName))
            {
                throw new ArgumentNullException("The value cannot be null or empty", nameof(serviceName));
            }

            if (string.IsNullOrEmpty(methodName))
            {
                throw new ArgumentNullException("The value cannot be null or empty", nameof(methodName));
            }

            if (data is null)
            {
                throw new ArgumentNullException("The value cannot be null", nameof(data));
            }

            var response = await this.MakeInvokeHttpRequest(serviceName, methodName, JsonSerializer.Serialize(data), cancellationToken).ConfigureAwait(false);

            if (response.Content == null || response.Content.Headers?.ContentLength == 0)
            {
                // If the invoke response is empty, then return.
                return default;
            }

            using (var stream = await response.Content.ReadAsStreamAsync())
            {
                return await JsonSerializer.DeserializeAsync<TResponse>(stream, this.serializerOptions, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Invokes the specified method on target service, Response data is deserialized using the default System.Text.Json Serialization.
        /// </summary>
        /// <param name="serviceName">The name of the service to be called in the Dapr invoke request.</param>
        /// <param name="methodName">THe name of the method to be called in the Dapr invoke request.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TResponse">The data type that the Dapr response body will be deserialized to.</typeparam>
        /// <returns>A <see cref="Task" /> that will return a deserialized object of the reponse from the Invoke request. If the Dapr response content is null the return type will be null.</returns>
        public override async Task<TResponse> InvokeMethodAsync<TResponse>(string serviceName, string methodName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(serviceName))
            {
                throw new ArgumentNullException("The value cannot be null or empty", nameof(serviceName));
            }

            if (string.IsNullOrEmpty(methodName))
            {
                throw new ArgumentNullException("The value cannot be null or empty", nameof(methodName));
            }

            var response = await this.MakeInvokeHttpRequest(serviceName, methodName, string.Empty, cancellationToken).ConfigureAwait(false);

            if (response.Content == null || response.Content.Headers?.ContentLength == 0)
            {
                // If the invoke response is empty, then return.
                return default;
            }

            using (var stream = await response.Content.ReadAsStreamAsync())
            {
                return await JsonSerializer.DeserializeAsync<TResponse>(stream, this.serializerOptions, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Invokes a method using the Dapr invoke endpoints.
        /// </summary>
        /// <param name="serviceName">The name of the service to be called in the Dapr invoke request.</param>
        /// <param name="methodName">THe name of the method to be called in the Dapr invoke request.</param>
        /// <param name="data">The data to be sent within the body of the Dapr invoke request.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TRequest">The data type of the object that will be serialized.</typeparam>
        /// <returns>A <see cref="Task" />.</returns>
        public override async Task InvokeMethodAsync<TRequest>(string serviceName, string methodName, TRequest data, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(serviceName))
            {
                throw new ArgumentNullException("The value cannot be null or empty", nameof(serviceName));
            }

            if (string.IsNullOrEmpty(methodName))
            {
                throw new ArgumentNullException("The value cannot be null or empty", nameof(methodName));
            }

            if (data is null)
            {
                throw new ArgumentNullException("The value cannot be null", nameof(data));
            }

            await this.MakeInvokeHttpRequest(serviceName, methodName, JsonSerializer.Serialize(data), cancellationToken).ConfigureAwait(false);
        }

        private async Task<HttpResponseMessage> MakeInvokeHttpRequest(string serviceName, string methodName, string data, CancellationToken cancellationToken)
        {
            var url = this.client.BaseAddress == null ? $"http://{DaprDefaultEndpoint}:{DefaultHttpPort}{InvokePath}/{serviceName}/method/{methodName}" : $"{InvokePath}/{serviceName}/method/{methodName}";
            var request = new HttpRequestMessage(HttpMethod.Post, url);

            request.Content = new StringContent(data);

            var response = await this.client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return default;
            }

            if (!response.IsSuccessStatusCode && response.Content != null)
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException($"Failed to invoke method with status code '{response.StatusCode}': {error}.");
            }
            else if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to invoke method with status code '{response.StatusCode}'.");
            }

            return response;
        }
    }
}