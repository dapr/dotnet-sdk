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

namespace Dapr.Actors.Test
{
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Security;
    using System.Security.Authentication;
    using System.Text.Json;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Xunit;

    /// <summary>
    /// Contains tests for DaprHttpInteractor.
    /// </summary>
    public class DaprHttpInteractorTest
    {
        [Fact]
        public async Task GetState_ValidateRequest()
        {
            await using var client = TestClient.CreateForDaprHttpInterator();

            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var keyName = "StateKey_Test";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                await httpInteractor.GetStateAsync(actorType, actorId, keyName);
            });

            request.Dismiss();

            var actualPath = request.Request.RequestUri.LocalPath.TrimStart('/');
            var expectedPath = string.Format(CultureInfo.InvariantCulture, Constants.ActorStateKeyRelativeUrlFormat, actorType, actorId, keyName);

            actualPath.Should().Be(expectedPath);
            request.Request.Method.Should().Be(HttpMethod.Get);
        }

        [Fact]
        public async Task SaveStateTransactionally_ValidateRequest()
        {
            await using var client = TestClient.CreateForDaprHttpInterator();

            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var data = "StateData";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                await httpInteractor.SaveStateTransactionallyAsync(actorType, actorId, data);
            });

            request.Dismiss();

            var actualPath = request.Request.RequestUri.LocalPath.TrimStart('/');
            var expectedPath = string.Format(CultureInfo.InvariantCulture, Constants.ActorStateRelativeUrlFormat, actorType, actorId);

            actualPath.Should().Be(expectedPath);
            request.Request.Method.Should().Be(HttpMethod.Put);
        }

        [Fact]
        public async Task InvokeActorMethodWithoutRemoting_ValidateRequest()
        {
            await using var client = TestClient.CreateForDaprHttpInterator();

            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var methodName = "MethodName";
            var payload = "JsonData";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                await httpInteractor.InvokeActorMethodWithoutRemotingAsync(actorType, actorId, methodName, payload);
            });

            request.Dismiss();

            var actualPath = request.Request.RequestUri.LocalPath.TrimStart('/');
            var expectedPath = string.Format(CultureInfo.InvariantCulture, Constants.ActorMethodRelativeUrlFormat, actorType, actorId, methodName);

            actualPath.Should().Be(expectedPath);
            request.Request.Method.Should().Be(HttpMethod.Put);
        }

        [Fact]
        public async Task RegisterReminder_ValidateRequest()
        {
            await using var client = TestClient.CreateForDaprHttpInterator();

            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var reminderName = "ReminderName";
            var payload = "JsonData";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                await httpInteractor.RegisterReminderAsync(actorType, actorId, reminderName, payload);
            });

            request.Dismiss();

            var actualPath = request.Request.RequestUri.LocalPath.TrimStart('/');
            var expectedPath = string.Format(CultureInfo.InvariantCulture, Constants.ActorReminderRelativeUrlFormat, actorType, actorId, reminderName);

            actualPath.Should().Be(expectedPath);
            request.Request.Method.Should().Be(HttpMethod.Put);
        }

        [Fact]
        public async Task UnregisterReminder_ValidateRequest()
        {
            await using var client = TestClient.CreateForDaprHttpInterator();

            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var reminderName = "ReminderName";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                await httpInteractor.UnregisterReminderAsync(actorType, actorId, reminderName);
            });

            request.Dismiss();

            var actualPath = request.Request.RequestUri.LocalPath.TrimStart('/');
            var expectedPath = string.Format(CultureInfo.InvariantCulture, Constants.ActorReminderRelativeUrlFormat, actorType, actorId, reminderName);

            actualPath.Should().Be(expectedPath);
            request.Request.Method.Should().Be(HttpMethod.Delete);
        }

        [Fact]
        public async Task RegisterTimer_ValidateRequest()
        {
            await using var client = TestClient.CreateForDaprHttpInterator();

            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var timerName = "TimerName";
            var payload = "JsonData";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                await httpInteractor.RegisterTimerAsync(actorType, actorId, timerName, payload);
            });

            request.Dismiss();

            var actualPath = request.Request.RequestUri.LocalPath.TrimStart('/');
            var expectedPath = string.Format(CultureInfo.InvariantCulture, Constants.ActorTimerRelativeUrlFormat, actorType, actorId, timerName);

            actualPath.Should().Be(expectedPath);
            request.Request.Method.Should().Be(HttpMethod.Put);
        }

        [Fact]
        public async Task UnregisterTimer_ValidateRequest()
        {
            await using var client = TestClient.CreateForDaprHttpInterator();

            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var timerName = "TimerName";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                await httpInteractor.UnregisterTimerAsync(actorType, actorId, timerName);
            });

            request.Dismiss();

            var actualPath = request.Request.RequestUri.LocalPath.TrimStart('/');
            var expectedPath = string.Format(CultureInfo.InvariantCulture, Constants.ActorTimerRelativeUrlFormat, actorType, actorId, timerName);

            actualPath.Should().Be(expectedPath);
            request.Request.Method.Should().Be(HttpMethod.Delete);
        }

        [Fact]
        public async Task Call_WithApiTokenSet()
        {
            await using var client = TestClient.CreateForDaprHttpInterator(apiToken: "test_token");

            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var timerName = "TimerName";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                await httpInteractor.UnregisterTimerAsync(actorType, actorId, timerName);
            });

            request.Dismiss();

            request.Request.Headers.TryGetValues("dapr-api-token", out var headerValues);
            headerValues.Count().Should().Be(1);
            headerValues.First().Should().Be("test_token");
        }

        [Fact]
        public async Task Call_WithoutApiToken()
        {
            await using var client = TestClient.CreateForDaprHttpInterator();

            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var timerName = "TimerName";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                await httpInteractor.UnregisterTimerAsync(actorType, actorId, timerName);
            });

            request.Dismiss();

            request.Request.Headers.TryGetValues("dapr-api-token", out var headerValues);
            headerValues.Should().BeNull();
        }

        [Fact]
        public async Task Call_ValidateUnsuccessfulResponse()
        {
            await using var client = TestClient.CreateForDaprHttpInterator();

            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var timerName = "TimerName";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                await httpInteractor.UnregisterTimerAsync(actorType, actorId, timerName);
            });

            request.Dismiss();

            var error = new DaprError()
            {
                ErrorCode = "ERR_STATE_STORE",
                Message = "State Store Error"
            };

            var message = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(JsonSerializer.Serialize(error))
            };

            await Assert.ThrowsAsync<DaprApiException>(async () =>
            {
                await request.CompleteAsync(message);
            });
        }

        [Fact]
        public async Task Call_ValidateUnsuccessful404Response()
        {
            await using var client = TestClient.CreateForDaprHttpInterator();

            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var timerName = "TimerName";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                await httpInteractor.UnregisterTimerAsync(actorType, actorId, timerName);
            });

            var message = new HttpResponseMessage(HttpStatusCode.NotFound);

            await Assert.ThrowsAsync<DaprApiException>(async () =>
            {
                await request.CompleteAsync(message);
            });
        }

        [Fact]
        public async Task Call_ValidateUnauthorizedResponse()
        {
            await using var client = TestClient.CreateForDaprHttpInterator();

            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var timerName = "TimerName";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                await httpInteractor.UnregisterTimerAsync(actorType, actorId, timerName);
            });

            var message = new HttpResponseMessage(HttpStatusCode.Unauthorized);

            await Assert.ThrowsAsync<AuthenticationException>(async () =>
            {
                await request.CompleteAsync(message);
            });
        }

        [Fact]
        public async Task InvokeActorMethodAddsReentrancyIdIfSet_ValidateHeaders()
        {
            await using var client = TestClient.CreateForDaprHttpInterator();

            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var methodName = "MethodName";
            var payload = "JsonData";

            ActorReentrancyContextAccessor.ReentrancyContext = "1";
            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                await httpInteractor.InvokeActorMethodWithoutRemotingAsync(actorType, actorId, methodName, payload);
            });

            request.Dismiss();
            Assert.True(request.Request.Headers.Contains(Constants.ReentrancyRequestHeaderName));
            Assert.Contains("1", request.Request.Headers.GetValues(Constants.ReentrancyRequestHeaderName));
        }

        [Fact]
        public async Task InvokeActorMethodOmitsReentrancyIdIfNotSet_ValidateHeaders()
        {
            await using var client = TestClient.CreateForDaprHttpInterator();

            var actorType = "ActorType_Test";
            var actorId = "ActorId_Test";
            var methodName = "MethodName";
            var payload = "JsonData";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                await httpInteractor.InvokeActorMethodWithoutRemotingAsync(actorType, actorId, methodName, payload);
            });

            request.Dismiss();
            Assert.False(request.Request.Headers.Contains(Constants.ReentrancyRequestHeaderName));
        }
    }
}
