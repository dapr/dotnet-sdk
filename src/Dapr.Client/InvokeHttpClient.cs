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
        /// Invokes a method using the Dapr invoke endpoints using the properties supplied in the  <paramref name="envelope" /> variable.
        /// </summary>
        /// <param name="envelope">The envelope containing the invoke request parameters.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The data type.</typeparam>
        /// <returns>A <see cref="Task" /> that will return a deserialized object of the reponse from the Invoke request.</returns>
        public override async Task<TValue> InvokeMethodAsync<TValue>(InvokeEnvelope envelope, CancellationToken cancellationToken = default)
        {
            if (envelope is null)
            {
                throw new ArgumentNullException("The value cannot be null or empty", nameof(envelope));
            }

            var url = this.client.BaseAddress == null ? $"http://localhost:{DefaultHttpPort}{InvokePath}/{envelope.ServiceName}/method/{envelope.MethodName}" : $"{InvokePath}/{envelope.ServiceName}/method/{envelope.MethodName}";
            var request = new HttpRequestMessage(HttpMethod.Post, url);

            request.Content = new StringContent(envelope.Data);

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

            if (response.Content == null || response.Content.Headers?.ContentLength == 0)
            {
                // The state store will return empty application/json instead of 204/404.
                return default;
            }

            using (var stream = await response.Content.ReadAsStreamAsync())
            {
                return await JsonSerializer.DeserializeAsync<TValue>(stream, this.serializerOptions, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}