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
    /// A client for interacting with the Dapr state store using <see cref="HttpClient" />.
    /// </summary>
    public sealed class StateHttpClient : StateClient
    {
        private readonly HttpClient client;
        private readonly JsonSerializerOptions serializerOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="StateHttpClient"/> class.
        /// </summary>
        /// <param name="client">The <see cref="HttpClient" />.</param>
        /// <param name="serializerOptions">The <see cref="JsonSerializerOptions" />.</param>
        public StateHttpClient(HttpClient client, JsonSerializerOptions serializerOptions = null)
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            this.client = client;
            this.serializerOptions = serializerOptions;
        }

        /// <summary>
        /// Gets the current value associated with the <paramref name="key" /> from the Dapr state store.
        /// </summary>
        /// <param name="storeName">The state store name.</param>
        /// <param name="key">The state key.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The data type.</typeparam>
        /// <returns>A <see cref="ValueTask" /> that will return the value when the operation has completed.</returns>
        public async override ValueTask<TValue> GetStateAsync<TValue>(string storeName, string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(storeName))
            {
                throw new ArgumentException("The value cannot be null or empty.", nameof(storeName));
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("The value cannot be null or empty.", nameof(key));
            }

            var url = this.client.BaseAddress == null ? $"http://localhost:{DefaultHttpPort}{StatePath}/{storeName}/{key}" : $"{StatePath}/{storeName}/{key}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await this.client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return default;
            }

            if (!response.IsSuccessStatusCode && response.Content != null)
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException($"Failed to get state with status code '{response.StatusCode}': {error}.");
            }
            else if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to get state with status code '{response.StatusCode}'.");
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

        /// <summary>
        /// Saves the provided <paramref name="value" /> associated with the provided <paramref name="key" /> to the Dapr state
        /// store.
        /// </summary>
        /// <param name="storeName">The state store name.</param>
        /// <param name="key">The state key.</param>
        /// <param name="value">The value to save.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <typeparam name="TValue">The data type.</typeparam>
        /// <returns>A <see cref="ValueTask" /> that will complete when the operation has completed.</returns>
        public async override ValueTask SaveStateAsync<TValue>(string storeName, string key, TValue value, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(storeName))
            {
                throw new ArgumentException("The value cannot be null or empty.", nameof(storeName));
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("The value cannot be null or empty.", nameof(key));
            }

            var url = this.client.BaseAddress == null ? $"http://localhost:{DefaultHttpPort}{StatePath}/{storeName}" : $"{StatePath}/{storeName}";
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            var obj = new object[] { new { key = key, value = value, } };
            request.Content = CreateContent(obj, this.serializerOptions);

            var response = await this.client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode && response.Content != null)
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException($"Failed to get state with status code '{response.StatusCode}': {error}.");
            }
            else if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to get state with status code '{response.StatusCode}'.");
            }
            else
            {
                return;
            }
        }

        /// <summary>
        /// Deletes the value associated with the provided <paramref name="key" /> in the Dapr state store.
        /// </summary>
        /// <param name="storeName">The state store name.</param>
        /// <param name="key">The state key.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask" /> that will complete when the operation has completed.</returns>
        public async override ValueTask DeleteStateAsync(string storeName, string key, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(storeName))
            {
                throw new ArgumentException("The value cannot be null or empty.", nameof(storeName));
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("The value cannot be null or empty.", nameof(key));
            }

            // Docs: https://github.com/dapr/docs/blob/master/reference/api/state.md#delete-state
            var url = this.client.BaseAddress == null ? $"http://localhost:{DefaultHttpPort}{StatePath}/{storeName}/{key}" : $"{StatePath}/{storeName}/{key}";
            var request = new HttpRequestMessage(HttpMethod.Delete, url);

            var response = await this.client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            // 200: success
            //
            // To avoid being overload coupled we handle a range of 2XX status codes in common use for DELETEs.
            if ((int)response.StatusCode >= 200 && (int)response.StatusCode <= 204)
            {
                return;
            }

            if (response.Content != null)
            {
                var error = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException($"Failed to delete state with status code '{response.StatusCode}': {error}.");
            }
            else
            {
                throw new HttpRequestException($"Failed to delete state with status code '{response.StatusCode}'.");
            }
        }

        private static AsyncJsonContent<T> CreateContent<T>(T obj, JsonSerializerOptions serializerOptions)
        {
            return new AsyncJsonContent<T>(obj, serializerOptions);
        }

        // Note: using push-streaming content here has a little higher cost for trivially-size payloads,
        // but avoids the significant allocation overhead in the cases where the content is really large.
        //
        // Similar to https://github.com/aspnet/AspNetWebStack/blob/master/src/System.Net.Http.Formatting/PushStreamContent.cs
        // but simplified because of async.
        private class AsyncJsonContent<T> : HttpContent
        {
            private readonly T obj;
            private readonly JsonSerializerOptions serializerOptions;

            public AsyncJsonContent(T obj, JsonSerializerOptions serializerOptions)
            {
                this.obj = obj;
                this.serializerOptions = serializerOptions;

                this.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "UTF-8", };
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                return JsonSerializer.SerializeAsync(stream, this.obj, this.serializerOptions);
            }

            protected override bool TryComputeLength(out long length)
            {
                // We can't know the length of the content being pushed to the output stream without doing
                // some writing.
                //
                // If we want to optimize this case, it could be done by implementing a custom stream
                // and then doing the first write to a fixed-size pooled byte array.
                //
                // HTTP is slightly more efficient when you can avoid using chunking (need to know Content-Length)
                // up front.
                length = -1;
                return false;
            }
        }
    }
}
