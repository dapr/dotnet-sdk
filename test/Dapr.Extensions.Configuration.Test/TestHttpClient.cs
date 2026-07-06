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

namespace Dapr;

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// This is an old piece of infrastructure with some limitations, don't use it in new places.
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

    public Action<Entry> Handler
    {
        get => this.handler.Handler;
        set => this.handler.Handler = value;
    }

    public class Entry
    {
        public Entry(HttpRequestMessage request)
        {
            this.Request = request;

            this.Completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public TaskCompletionSource<HttpResponseMessage> Completion { get; }

        public HttpRequestMessage Request { get; }

        public void Respond(HttpResponseMessage response)
        {
            this.Completion.SetResult(response);
        }

        public void RespondWithResponse(HttpResponseMessage response)
        {
            this.Completion.SetResult(response);
        }

        public void RespondWithJson<TValue>(TValue value, JsonSerializerOptions options = null)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, options);

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "UTF-8", };

            this.Completion.SetResult(response);
        }

        public void Throw(Exception exception)
        {
            this.Completion.SetException(exception);
        }
    }

    private class TestHttpClientHandler : HttpMessageHandler
    {
        public TestHttpClientHandler()
        {
            this.Requests = new ConcurrentQueue<Entry>();
        }

        public ConcurrentQueue<Entry> Requests { get; }

        public Action<Entry> Handler { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var entry = new Entry(request);
            this.Handler?.Invoke(entry);
            this.Requests.Enqueue(entry);

            using (cancellationToken.Register(() => entry.Completion.TrySetCanceled()))
            {
                return await entry.Completion.Task.ConfigureAwait(false);
            }
        }
    }
}