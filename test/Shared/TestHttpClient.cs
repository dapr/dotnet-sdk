// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Dapr
{
    using System.Collections.Concurrent;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    // This client will capture all requests, and put them in .Requests for you to inspect.
    public class TestHttpClient : HttpClient
    {
        private readonly TestHttpClientHandler handler;

        public TestHttpClient()
            : this(new TestHttpClientHandler())
        {
        }

        private TestHttpClient(TestHttpClientHandler handler)
            : base(handler)
        {
            this.handler = handler;
        }

        public ConcurrentQueue<Entry> Requests => this.handler.Requests;

        public class Entry
        {
            public Entry(HttpRequestMessage request)
            {
                Request = request;

                Completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public TaskCompletionSource<HttpResponseMessage> Completion { get; }

            public HttpRequestMessage Request { get; }

            public void Respond(HttpResponseMessage response)
            {
                Completion.SetResult(response);
            }

            public void RespondWithJson<TValue>(TValue value, JsonSerializerOptions options = null)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(value, options);

                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new ByteArrayContent(bytes);
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "UTF-8", };

                Completion.SetResult(response);
            }
        }

        private class TestHttpClientHandler : HttpMessageHandler
        {
            public TestHttpClientHandler()
            {
                Requests = new ConcurrentQueue<Entry>();
            }

            public ConcurrentQueue<Entry> Requests { get; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var entry = new Entry(request);
                Requests.Enqueue(entry);

                using (cancellationToken.Register(() => entry.Completion.TrySetCanceled()))
                {
                    return await entry.Completion.Task.ConfigureAwait(false);
                }
            }
        }
    }
}