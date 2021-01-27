// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Client;
using FluentAssertions;
using Xunit;

namespace Dapr.Actors.Test
{
    public class ApiTokenTests
    {
        [Fact]
        public void CreateProxyWithRemoting_WithApiToken()
        {
            var actorId = new ActorId("abc");
            var handler = new TestHttpClientHandler();
            var options = new ActorProxyOptions
            {
                DaprApiToken = "test_token",
            };
            var factory = new ActorProxyFactory(options, handler);
            var proxy = factory.CreateActorProxy<ITestActor>(actorId, "TestActor");
            var task = proxy.SetCountAsync(1, new CancellationToken());

            handler.Requests.TryDequeue(out var entry).Should().BeTrue();
            var headerValues = entry.Request.Headers.GetValues("dapr-api-token");
            headerValues.Should().Contain("test_token");
        }

        [Fact]
        public void CreateProxyWithRemoting_WithNoApiToken()
        {
            var actorId = new ActorId("abc");
            var handler = new TestHttpClientHandler();
            var factory = new ActorProxyFactory(null, handler);
            var proxy = factory.CreateActorProxy<ITestActor>(actorId, "TestActor");
            var task = proxy.SetCountAsync(1, new CancellationToken());

            handler.Requests.TryDequeue(out var entry).Should().BeTrue();
            Action action = () => entry.Request.Headers.GetValues("dapr-api-token");
            action.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void CreateProxyWithNoRemoting_WithApiToken()
        {
            var actorId = new ActorId("abc");
            var handler = new TestHttpClientHandler();
            var options = new ActorProxyOptions
            {
                DaprApiToken = "test_token",
            };
            var factory = new ActorProxyFactory(options, handler);
            var proxy = factory.Create(actorId, "TestActor");
            var task = proxy.InvokeMethodAsync("SetCountAsync", 1, new CancellationToken());

            handler.Requests.TryDequeue(out var entry).Should().BeTrue();
            var headerValues = entry.Request.Headers.GetValues("dapr-api-token");
            headerValues.Should().Contain("test_token");
        }

        [Fact]
        public void CreateProxyWithNoRemoting_WithNoApiToken()
        {
            var actorId = new ActorId("abc");
            var handler = new TestHttpClientHandler();
            var factory = new ActorProxyFactory(null, handler);
            var proxy = factory.Create(actorId, "TestActor");
            var task = proxy.InvokeMethodAsync("SetCountAsync", 1, new CancellationToken());

            handler.Requests.TryDequeue(out var entry).Should().BeTrue();
            Action action = () => entry.Request.Headers.GetValues("dapr-api-token");
            action.Should().Throw<InvalidOperationException>();
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
        }

        private class TestHttpClientHandler : HttpClientHandler
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
}
