// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using static Dapr.DaprUris;

    /// <summary>
    /// A client for interacting with the Dapr secret store using <see cref="HttpClient" />.
    /// </summary>
    public sealed class SecretHttpClient : SecretClient
    {
        private const string DaprDefaultEndpoint = "127.0.0.1";
        private readonly HttpClient client;
        private readonly JsonSerializerOptions serializerOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="StateHttpClient"/> class.
        /// </summary>
        /// <param name="client">The <see cref="HttpClient" />.</param>
        /// <param name="serializerOptions">The <see cref="JsonSerializerOptions" />.</param>
        public SecretHttpClient(HttpClient client, JsonSerializerOptions serializerOptions = null)
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            this.client = client;
            this.serializerOptions = serializerOptions;
        }

        /// <summary>
        /// Gets the current value associated with the <paramref name="secretName" /> from the Dapr secret store.
        /// </summary>
        /// <param name="storeName">The secret store name.</param>
        /// <param name="secretName">The secret name.</param>
        /// <param name="metadata">The secret metadata.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask" /> that will return the value when the operation has completed.</returns>
        public async override ValueTask<Dictionary<string, string>> GetSecretAsync(string storeName, string secretName, Dictionary<string, string> metadata, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(storeName))
            {
                throw new ArgumentException("The value cannot be null or empty.", nameof(storeName));
            }

            if (string.IsNullOrEmpty(secretName))
            {
                throw new ArgumentException("The value cannot be null or empty.", nameof(secretName));
            }

            var metadataString = string.Empty;
            if (metadata != null && metadata.Count > 0)
            {
                foreach (var kv in metadata)
                {
                    metadataString += $"{kv.Key}={kv.Value}&";
                }
                metadataString = $"?{metadataString.TrimEnd('&')}";
            }

            var url = this.client.BaseAddress == null ? $"http://{DaprDefaultEndpoint}:{DefaultHttpPort}{SecretPath}/{storeName}/{secretName}{metadata}" : $"{SecretPath}/{storeName}/{secretName}{metadata}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await this.client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return default;
            }

            if (!response.IsSuccessStatusCode && response.Content != null)
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException($"Failed to get secret with status code '{response.StatusCode}': {error}.");
            }
            else if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to get secrfet with status code '{response.StatusCode}'.");
            }

            if (response.Content == null || response.Content.Headers?.ContentLength == 0)
            {
                // The secret store will return empty application/json instead of 204/404.
                return default;
            }

            using (var stream = await response.Content.ReadAsStreamAsync())
            {
                return await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, this.serializerOptions, cancellationToken).ConfigureAwait(false);
            }
        }

    }
}
