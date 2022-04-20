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

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
#if ACTORS
using Dapr.Actors;
#endif
using Dapr.Client;
using Google.Protobuf;
using Grpc.Net.Client;

namespace Dapr
{
    public abstract class TestClient
    {
        #if ACTORS
        internal static TestClient<DaprHttpInteractor> CreateForDaprHttpInterator(string? apiToken = null)
        {
            var handler = new CapturingHandler();
            return new TestClient<DaprHttpInteractor>(new DaprHttpInteractor(handler, "http://localhost:3500", apiToken, null), handler);
        }
        #endif

        public static TestClient<HttpMessageHandler> CreateForMessageHandler()
        {
            var handler = new CapturingHandler();
            return new TestClient<HttpMessageHandler>(handler, handler);
        }

        public static TestClient<DaprClient> CreateForDaprClient(Action<DaprClientBuilder>? configure = default)
        {
            var handler = new CapturingHandler();
            var httpClient = new HttpClient(handler);

            var builder = new DaprClientBuilder();
            configure?.Invoke(builder);

            builder.UseHttpClientFactory(() => httpClient);
            builder.UseGrpcChannelOptions(new GrpcChannelOptions()
            {
                HttpClient = httpClient,
                ThrowOperationCanceledOnCancellation = true,
            });

            return new TestClient<DaprClient>(builder.Build(), handler);
        }

        private static async Task WithTimeout(Task task, TimeSpan timeout, string message)
        {
            var tcs = new TaskCompletionSource<object>();

            using var cts = new CancellationTokenSource(timeout);
            using (cts.Token.Register((obj) =>
            {
                var tcs = (TaskCompletionSource<object>)obj!;
                tcs.SetException(new TimeoutException());
            }, tcs))
            {
                await (await Task.WhenAny(task, tcs.Task));
            }
        }

        private static async Task<T> WithTimeout<T>(Task<T> task, TimeSpan timeout, string message)
        {
            var tcs = new TaskCompletionSource<T>();

            using var cts = new CancellationTokenSource(timeout);
            using (cts.Token.Register((obj) =>
            {
                var tcs = (TaskCompletionSource<T>)obj!;
                tcs.SetException(new TimeoutException());
            }, tcs))
            {
                return await (await Task.WhenAny<T>(task, tcs.Task));
            }
        }

        public class TestHttpRequest
        {
            public TestHttpRequest(HttpRequestMessage request, CaptureToken capture, Task task)
            {
                this.Request = request;
                this.Capture = capture;
                this.Task = task;
            }

            public HttpRequestMessage Request { get; }

            private CaptureToken Capture { get; }

            private Task Task { get; }

            public async Task CompleteAsync(HttpResponseMessage response)
            {
                this.Capture.Response.SetResult(response);
                await WithTimeout(this.Task, TimeSpan.FromSeconds(10), "Waiting for response to be completed timed out.");
            }

            public async Task CompleteWithExceptionAsync(Exception ex)
            {
                this.Capture.Response.SetException(ex);
                await WithTimeout(this.Task, TimeSpan.FromSeconds(10), "Waiting for response to be completed timed out.");
            }

            public void Dismiss()
            {
                this.Capture.IsDismissed = true;
            }
        }

        public class TestHttpRequest<T>
        {
            public TestHttpRequest(HttpRequestMessage request, CaptureToken capture, Task<T> task)
            {
                this.Request = request;
                this.Capture = capture;
                this.Task = task;
            }

            public HttpRequestMessage Request { get; }

            private CaptureToken Capture { get; }

            private Task<T> Task { get; }

            public async Task<T> CompleteWithJsonAsync<TData>(TData value, JsonSerializerOptions options)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(value, options);
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(bytes)
                };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "UTF-8", };
                return await CompleteAsync(response);
            }

            public async Task<T> CompleteAsync(HttpResponseMessage response)
            {
                this.Capture.Response.SetResult(response);
                return await WithTimeout<T>(this.Task, TimeSpan.FromSeconds(10), "Waiting for response to be completed timed out.");
            }

            public async Task CompleteWithExceptionAsync(Exception ex)
            {
                this.Capture.Response.SetException(ex);
                await WithTimeout(this.Task, TimeSpan.FromSeconds(10), "Waiting for response to be completed timed out.");
            }
            
            public async Task<T> CompleteWithExceptionAndResultAsync(Exception ex)
            {
                this.Capture.Response.SetException(ex);
                return await WithTimeout(this.Task, TimeSpan.FromSeconds(10), "Waiting for response to be completed timed out.");
            }

            public void Dismiss()
            {
                this.Capture.IsDismissed = true;
            }
        }

        public class TestGrpcRequest
        {
            public TestGrpcRequest(HttpRequestMessage request, CaptureToken capture, Task task)
            {
                this.Request = request;
                this.Capture = capture;
                this.Task = task;
            }

            public HttpRequestMessage Request { get; }

            private CaptureToken Capture { get; }

            private Task Task { get; }

            public async Task<TRequestEnvelope> GetRequestEnvelopeAsync<TRequestEnvelope>()
                where TRequestEnvelope : IMessage<TRequestEnvelope>, new()
            {
                return await GrpcUtils.GetRequestFromRequestMessageAsync<TRequestEnvelope>(this.Request);
            }

            public async Task CompleteWithMessageAsync<TResponseEnvelope>(TResponseEnvelope value)
                where TResponseEnvelope : IMessage<TResponseEnvelope>
            {
                var content = await GrpcUtils.CreateResponseContent<TResponseEnvelope>(value);
                var response = GrpcUtils.CreateResponse(HttpStatusCode.OK, content);
                await CompleteAsync(response);
            }

            public async Task CompleteAsync(HttpResponseMessage response)
            {
                this.Capture.Response.SetResult(response);
                await WithTimeout(this.Task, TimeSpan.FromSeconds(10), "Waiting for response to be completed timed out.");
            }

            public async Task CompleteWithExceptionAsync(Exception ex)
            {
                this.Capture.Response.SetException(ex);
                await WithTimeout(this.Task, TimeSpan.FromSeconds(10), "Waiting for response to be completed timed out.");
            }

            public void Dismiss()
            {
                this.Capture.IsDismissed = true;
            }
        }

        public class TestGrpcRequest<TResponse>
        {
            public TestGrpcRequest(HttpRequestMessage request, CaptureToken capture, Task<TResponse> task)
            {
                this.Request = request;
                this.Capture = capture;
                this.Task = task;
            }

            public HttpRequestMessage Request { get; }

            private CaptureToken Capture { get; }

            private Task<TResponse> Task { get; }

            public async Task<TRequestEnvelope> GetRequestEnvelopeAsync<TRequestEnvelope>()
                where TRequestEnvelope : IMessage<TRequestEnvelope>, new()
            {
                return await GrpcUtils.GetRequestFromRequestMessageAsync<TRequestEnvelope>(this.Request);
            }

            public async Task<TResponse> CompleteWithMessageAsync<TResponseEnvelope>(TResponseEnvelope value)
                where TResponseEnvelope : IMessage<TResponseEnvelope>
            {
                var content = await GrpcUtils.CreateResponseContent<TResponseEnvelope>(value);
                var response = GrpcUtils.CreateResponse(HttpStatusCode.OK, content);
                return await CompleteAsync(response);
            }

            public async Task<TResponse> CompleteAsync(HttpResponseMessage response)
            {
                this.Capture.Response.SetResult(response);
                return await WithTimeout<TResponse>(this.Task, TimeSpan.FromSeconds(10), "Waiting for response to be completed timed out.");
            }

            public async Task CompleteWithExceptionAsync(Exception ex)
            {
                this.Capture.Response.SetException(ex);
                await WithTimeout(this.Task, TimeSpan.FromSeconds(10), "Waiting for response to be completed timed out.");
            }

            public void Dismiss()
            {
                this.Capture.IsDismissed = true;
            }
        }

        public class CapturingHandler : HttpMessageHandler
        {
            private readonly ConcurrentQueue<CaptureToken> requests = new ConcurrentQueue<CaptureToken>();
            private readonly object @lock = new object();
            private CaptureToken? current;
            public CaptureToken Capture()
            {
                lock (this.@lock)
                {
                    if (this.current is CaptureToken)
                    {
                        throw new InvalidOperationException(
                            "Capture operation started while already capturing. " +
                            "Concurrent use of the test client is not supported.");
                    }

                    return (this.current = new CaptureToken());
                }
            }

            public IEnumerable<CaptureToken> GetOutstandingRequests()
            {
                foreach (var request in this.requests)
                {
                    if (request.IsComplete)
                    {
                        continue;
                    }

                    yield return request;
                }
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CaptureToken? capture;
                lock (this.@lock)
                {
                    if ((capture = this.current) is CaptureToken)
                    {
                        this.current = default;
                    }
                }

                capture ??= new CaptureToken();
                this.requests.Enqueue(capture);
                capture.Request.SetResult(request);

                return capture.Response.Task;
            }
        }

        public class CaptureToken
        {
            public TaskCompletionSource<HttpRequestMessage> Request { get; } = new TaskCompletionSource<HttpRequestMessage>();

            public TaskCompletionSource<HttpResponseMessage> Response { get; } = new TaskCompletionSource<HttpResponseMessage>();

            public bool IsDismissed { get; set; }

            public bool IsComplete
            {
                get
                {
                    return
                        IsDismissed ||
                        // We assume that whomever started the work observed exceptions making the request.
                        !Request.Task.IsCompletedSuccessfully ||
                        Response.Task.IsCompleted;
                }
            }

            public Task<HttpRequestMessage> GetRequestAsync(TimeSpan timeout)
            {
                return WithTimeout(Request.Task, timeout, "Waiting for request to be queued timed out.");
            }
        }
    }

    public class TestClient<TClient> : TestClient, IAsyncDisposable
    {
        public TestClient(TClient innerClient, CapturingHandler handler)
        {
            this.InnerClient = innerClient;
            this.Handler = handler;
        }

        public TClient InnerClient { get; }

        private CapturingHandler Handler { get; }

        public async Task<TestHttpRequest> CaptureHttpRequestAsync(Func<TClient, Task> operation)
        {
            var (request, capture, task) = await CaptureHttpRequestMessageAsync(operation);
            return new TestHttpRequest(request, capture, task);
        }

        public async Task<TestHttpRequest<T>> CaptureHttpRequestAsync<T>(Func<TClient, Task<T>> operation)
        {
            var (request, capture, task) = await CaptureHttpRequestMessageAsync(operation);
            return new TestHttpRequest<T>(request, capture, (Task<T>)task);
        }

        public async Task<TestGrpcRequest> CaptureGrpcRequestAsync(Func<TClient, Task> operation)
        {
            var (request, capture, task) = await CaptureHttpRequestMessageAsync(operation);
            return new TestGrpcRequest(request, capture, task);
        }

        public async Task<TestGrpcRequest<T>> CaptureGrpcRequestAsync<T>(Func<TClient, Task<T>> operation)
        {
            var (request, capture, task) = await CaptureHttpRequestMessageAsync(operation);
            return new TestGrpcRequest<T>(request, capture, (Task<T>)task);
        }

        private async Task<(HttpRequestMessage, CaptureToken, Task)> CaptureHttpRequestMessageAsync(Func<TClient, Task> operation)
        {
            var capture = this.Handler.Capture();
            var task = operation(this.InnerClient);
            if (task.IsFaulted)
            {
                // If the task throws, we want to bubble that up eaglerly.
                await task;
            }

            HttpRequestMessage request;
            try
            {
                // Apply a 10 second timeout to waiting for the task to be queued. This is a very
                // generous timeout so if we hit it then it's likely a bug.
                request = await capture.GetRequestAsync(TimeSpan.FromSeconds(10));
            }

            // If the original operation threw, report that instead of the timeout
            catch (TimeoutException) when (task.IsFaulted)
            {
                await task;
                throw; // unreachable
            }

            return (request, capture, task);
        }

        public ValueTask DisposeAsync()
        {
            (this.InnerClient as IDisposable)?.Dispose();

            var requests = this.Handler.GetOutstandingRequests().ToArray();
            if (requests.Length > 0)
            {
                throw new InvalidOperationException(
                    "The client has 1 or more incomplete requests. " +
                    "Use 'request.Dismiss()' if the test is uninterested in the response.");
            }

            return new ValueTask();
        }
    }
}
