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
    /// A client for interacting with the Dapr publish endpoints using <see cref="HttpClient" />.
    /// </summary>
    public class PublishHttpClient : PublishClient
    {
        private const string DaprDefaultEndpoint = "127.0.0.1";
        private readonly HttpClient client;
        private readonly JsonSerializerOptions serializerOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="PublishHttpClient"/> class.
        /// </summary>
        /// <param name="client">The <see cref="HttpClient" />.</param>
        /// <param name="serializerOptions">The <see cref="JsonSerializerOptions" />.</param>
        public PublishHttpClient(HttpClient client, JsonSerializerOptions serializerOptions = null)
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            this.client = client;
            this.serializerOptions = serializerOptions;
        }

        /// <inheritdoc/>
        public override async Task PublishEventAsync<TRequest>(string topicName, TRequest publishContent, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(topicName))
            {
                throw new ArgumentException("The value cannot be null or empty", nameof(topicName));
            }

            if (publishContent is null)
            {
                throw new ArgumentNullException(nameof(publishContent));
            }

            await this.MakePublishHttpRequest<TRequest>(topicName, publishContent, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public override async Task PublishEventAsync(string topicName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(topicName))
            {
                throw new ArgumentException("The value cannot be null or empty", nameof(topicName));
            }

            await this.MakePublishHttpRequest<string>(topicName, string.Empty, cancellationToken).ConfigureAwait(false);
        }

        private async Task<HttpResponseMessage> MakePublishHttpRequest<TRequest>(string topicName, TRequest publishContents, CancellationToken cancellationToken)
        {
            var url = this.client.BaseAddress == null ? $"http://{DaprDefaultEndpoint}:{DefaultHttpPort}{PublishPath}/{topicName}" : $"{PublishPath}/{topicName}";
            var request = new HttpRequestMessage(HttpMethod.Post, url);

            if (publishContents != null)
            {
                request.Content = AsyncJsonContent<TRequest>.CreateContent(publishContents, this.serializerOptions);
            }

            var response = await this.client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode && response.Content != null)
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException($"Failed to publish event with status code '{response.StatusCode}': {error}.");
            }
            else if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to publish event with status code '{response.StatusCode}'.");
            }

            return response;
        }
    }
}