// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Test
{
    using System;
    using System.Collections.Concurrent;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Security.Authentication;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Contains tests for DaprHttpInteractor.
    /// </summary>
    public class DaprHttpInteractorTest
    {
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

        [Fact]
        public void GetState_ValidateRequest()
        {
            var handler = new TestHttpClientHandler();
            var httpInteractor = new DaprHttpInteractor(handler);
            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var keyName = "StateKey_Test";

            var task = httpInteractor.GetStateAsync(actorType, actorId, keyName);

            handler.Requests.TryDequeue(out var entry).Should().BeTrue();
            var actualPath = entry.Request.RequestUri.LocalPath.TrimStart('/');
            var expectedPath = string.Format(CultureInfo.InvariantCulture, Constants.ActorStateKeyRelativeUrlFormat, actorType, actorId, keyName);

            actualPath.Should().Be(expectedPath);
            entry.Request.Method.Should().Be(HttpMethod.Get);
        }

        [Fact]
        public void SaveStateTransactionally_ValidateRequest()
        {
            var handler = new TestHttpClientHandler();
            var httpInteractor = new DaprHttpInteractor(handler);
            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var data = "StateData";

            var task = httpInteractor.SaveStateTransactionallyAsync(actorType, actorId, data);

            handler.Requests.TryDequeue(out var entry).Should().BeTrue();
            var actualPath = entry.Request.RequestUri.LocalPath.TrimStart('/');
            var expectedPath = string.Format(CultureInfo.InvariantCulture, Constants.ActorStateRelativeUrlFormat, actorType, actorId);

            actualPath.Should().Be(expectedPath);
            entry.Request.Method.Should().Be(HttpMethod.Put);
        }

        [Fact]
        public void InvokeActorMethodWithoutRemoting_ValidateRequest()
        {
            var handler = new TestHttpClientHandler();
            var httpInteractor = new DaprHttpInteractor(handler);
            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var methodName = "MethodName";
            var payload = "JsonData";

            var task = httpInteractor.InvokeActorMethodWithoutRemotingAsync(actorType, actorId, methodName, payload);

            handler.Requests.TryDequeue(out var entry).Should().BeTrue();
            var actualPath = entry.Request.RequestUri.LocalPath.TrimStart('/');
            var expectedPath = string.Format(CultureInfo.InvariantCulture, Constants.ActorMethodRelativeUrlFormat, actorType, actorId, methodName);

            actualPath.Should().Be(expectedPath);
            entry.Request.Method.Should().Be(HttpMethod.Put);
        }

        [Fact]
        public void RegisterReminder_ValidateRequest()
        {
            var handler = new TestHttpClientHandler();
            var httpInteractor = new DaprHttpInteractor(handler);
            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var reminderName = "ReminderName";
            var payload = "JsonData";

            var task = httpInteractor.RegisterReminderAsync(actorType, actorId, reminderName, payload);

            handler.Requests.TryDequeue(out var entry).Should().BeTrue();
            var actualPath = entry.Request.RequestUri.LocalPath.TrimStart('/');
            var expectedPath = string.Format(CultureInfo.InvariantCulture, Constants.ActorReminderRelativeUrlFormat, actorType, actorId, reminderName);

            actualPath.Should().Be(expectedPath);
            entry.Request.Method.Should().Be(HttpMethod.Put);
        }

        [Fact]
        public void UnregisterReminder_ValidateRequest()
        {
            var handler = new TestHttpClientHandler();
            var httpInteractor = new DaprHttpInteractor(handler);
            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var reminderName = "ReminderName";

            var task = httpInteractor.UnregisterReminderAsync(actorType, actorId, reminderName);

            handler.Requests.TryDequeue(out var entry).Should().BeTrue();
            var actualPath = entry.Request.RequestUri.LocalPath.TrimStart('/');
            var expectedPath = string.Format(CultureInfo.InvariantCulture, Constants.ActorReminderRelativeUrlFormat, actorType, actorId, reminderName);

            actualPath.Should().Be(expectedPath);
            entry.Request.Method.Should().Be(HttpMethod.Delete);
        }

        [Fact]
        public void RegisterTimer_ValidateRequest()
        {
            var handler = new TestHttpClientHandler();
            var httpInteractor = new DaprHttpInteractor(handler);
            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var timerName = "TimerName";
            var payload = "JsonData";

            var task = httpInteractor.RegisterTimerAsync(actorType, actorId, timerName, payload);

            handler.Requests.TryDequeue(out var entry).Should().BeTrue();
            var actualPath = entry.Request.RequestUri.LocalPath.TrimStart('/');
            var expectedPath = string.Format(CultureInfo.InvariantCulture, Constants.ActorTimerRelativeUrlFormat, actorType, actorId, timerName);

            actualPath.Should().Be(expectedPath);
            entry.Request.Method.Should().Be(HttpMethod.Put);
        }

        [Fact]
        public void UnregisterTimer_ValidateRequest()
        {
            var handler = new TestHttpClientHandler();
            var httpInteractor = new DaprHttpInteractor(handler);
            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var timerName = "TimerName";

            var task = httpInteractor.UnregisterTimerAsync(actorType, actorId, timerName);

            handler.Requests.TryDequeue(out var entry).Should().BeTrue();
            var actualPath = entry.Request.RequestUri.LocalPath.TrimStart('/');
            var expectedPath = string.Format(CultureInfo.InvariantCulture, Constants.ActorTimerRelativeUrlFormat, actorType, actorId, timerName);

            actualPath.Should().Be(expectedPath);
            entry.Request.Method.Should().Be(HttpMethod.Delete);
        }

        [Fact]
        public void Call_WithApiTokenSet()
        {
            var handler = new TestHttpClientHandler();
            var httpInteractor = new DaprHttpInteractor(handler, apiToken: "test_token");
            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var timerName = "TimerName";

            var task = httpInteractor.UnregisterTimerAsync(actorType, actorId, timerName);

            handler.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.Headers.TryGetValues("dapr-api-token", out var headerValues);
            headerValues.Count().Should().Be(1);
            headerValues.First().Should().Be("test_token");
        }

        [Fact]
        public void Call_WithoutApiToken()
        {
            var handler = new TestHttpClientHandler();
            var httpInteractor = new DaprHttpInteractor(handler);
            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var timerName = "TimerName";

            var task = httpInteractor.UnregisterTimerAsync(actorType, actorId, timerName);

            handler.Requests.TryDequeue(out var entry).Should().BeTrue();
            entry.Request.Headers.TryGetValues("dapr-api-token", out var headerValues);
            headerValues.Should().BeNull();
        }

        [Fact]
        public async Task Call_ValidateUnsuccessfulResponse()
        {
            var handler = new TestHttpClientHandler();
            var httpInteractor = new DaprHttpInteractor(handler);
            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var timerName = "TimerName";

            var task = httpInteractor.UnregisterTimerAsync(actorType, actorId, timerName);
            handler.Requests.TryDequeue(out var entry).Should().BeTrue();

            var error = new DaprError()
            {
                ErrorCode = "ERR_STATE_STORE",
                Message = "State Store Error"
            };

            var message = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(JsonSerializer.Serialize(error))
            };

            entry.Completion.SetResult(message);
            await FluentActions.Awaiting(async () => await task).Should().ThrowAsync<DaprApiException>();
        }

        [Fact]
        public async Task Call_ValidateUnsuccessful404Response()
        {
            var handler = new TestHttpClientHandler();
            var httpInteractor = new DaprHttpInteractor(handler);
            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var timerName = "TimerName";

            var task = httpInteractor.UnregisterTimerAsync(actorType, actorId, timerName);
            handler.Requests.TryDequeue(out var entry).Should().BeTrue();

            var message = new HttpResponseMessage(HttpStatusCode.NotFound);

            entry.Completion.SetResult(message);
            await FluentActions.Awaiting(async () => await task).Should().ThrowAsync<DaprApiException>();
        }

        [Fact]
        public async Task Call_ValidateUnauthorizedResponse()
        {
            var handler = new TestHttpClientHandler();
            var httpInteractor = new DaprHttpInteractor(handler);
            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var timerName = "TimerName";

            var task = httpInteractor.UnregisterTimerAsync(actorType, actorId, timerName);
            handler.Requests.TryDequeue(out var entry).Should().BeTrue();

            var message = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            entry.Completion.SetResult(message);
            await FluentActions.Awaiting(async () => await task).Should().ThrowAsync<AuthenticationException>();
        }
    }
}
