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
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Security.Authentication;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Shouldly;
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

            const string actorType = "ActorType_Test";
            const string actorId = "ActorId_Test";
            const string keyName = "StateKey_Test";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                await httpInteractor.GetStateAsync(actorType, actorId, keyName);
            });

            request.Dismiss();

            var actualPath = request.Request.RequestUri.LocalPath.TrimStart('/');
            var expectedPath = string.Format(CultureInfo.InvariantCulture, Constants.ActorStateKeyRelativeUrlFormat, actorType, actorId, keyName);

            actualPath.ShouldBe(expectedPath);
            request.Request.Method.ShouldBe(HttpMethod.Get);
        }

        [Fact]
        public async Task SaveStateTransactionally_ValidateRequest()
        {
            await using var client = TestClient.CreateForDaprHttpInterator();

            const string actorType = "ActorType_Test";
            const string actorId = "ActorId_Test";
            const string data = "StateData";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                await httpInteractor.SaveStateTransactionallyAsync(actorType, actorId, data);
            });

            request.Dismiss();

            var actualPath = request.Request.RequestUri.LocalPath.TrimStart('/');
            var expectedPath = string.Format(CultureInfo.InvariantCulture, Constants.ActorStateRelativeUrlFormat, actorType, actorId);

            actualPath.ShouldBe(expectedPath);
            request.Request.Method.ShouldBe(HttpMethod.Put);
        }

        [Fact]
        public async Task InvokeActorMethodWithoutRemoting_ValidateRequest()
        {
            await using var client = TestClient.CreateForDaprHttpInterator();

            const string actorType = "ActorType_Test";
            const string actorId = "ActorId_Test";
            const string methodName = "MethodName";
            const string payload = "JsonData";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                await httpInteractor.InvokeActorMethodWithoutRemotingAsync(actorType, actorId, methodName, payload);
            });

            request.Dismiss();

            var actualPath = request.Request.RequestUri.LocalPath.TrimStart('/');
            var expectedPath = string.Format(CultureInfo.InvariantCulture, Constants.ActorMethodRelativeUrlFormat, actorType, actorId, methodName);

            actualPath.ShouldBe(expectedPath);
            request.Request.Method.ShouldBe(HttpMethod.Put);
        }

        [Fact]
        public async Task RegisterReminder_ValidateRequest()
        {
            await using var client = TestClient.CreateForDaprHttpInterator();

            const string actorType = "ActorType_Test";
            const string actorId = "ActorId_Test";
            const string reminderName = "ReminderName";
            const string payload = "JsonData";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                await httpInteractor.RegisterReminderAsync(actorType, actorId, reminderName, payload);
            });

            request.Dismiss();

            var actualPath = request.Request.RequestUri.LocalPath.TrimStart('/');
            var expectedPath = string.Format(CultureInfo.InvariantCulture, Constants.ActorReminderRelativeUrlFormat, actorType, actorId, reminderName);

            actualPath.ShouldBe(expectedPath);
            request.Request.Method.ShouldBe(HttpMethod.Put);
        }

        [Fact]
        public async Task UnregisterReminder_ValidateRequest()
        {
            await using var client = TestClient.CreateForDaprHttpInterator();

            const string actorType = "ActorType_Test";
            const string actorId = "ActorId_Test";
            const string reminderName = "ReminderName";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                await httpInteractor.UnregisterReminderAsync(actorType, actorId, reminderName);
            });

            request.Dismiss();

            var actualPath = request.Request.RequestUri.LocalPath.TrimStart('/');
            var expectedPath = string.Format(CultureInfo.InvariantCulture, Constants.ActorReminderRelativeUrlFormat, actorType, actorId, reminderName);

            actualPath.ShouldBe(expectedPath);
            request.Request.Method.ShouldBe(HttpMethod.Delete);
        }

        [Fact]
        public async Task GetReminder_ValidateRequest()
        {
            await using var client = TestClient.CreateForDaprHttpInterator();
            
            const string actorType = "ActorType_Test";
            const string actorId = "ActorId_Test";
            const string reminderName = "ReminderName";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                await httpInteractor.GetReminderAsync(actorType, actorId, reminderName);
            });

            request.Dismiss();

            var actualPath = request.Request.RequestUri.LocalPath.TrimStart('/');
            var expectedPath = string.Format(CultureInfo.InvariantCulture, Constants.ActorReminderRelativeUrlFormat,
                actorType, actorId, reminderName);

            actualPath.ShouldBe(expectedPath);
            request.Request.Method.ShouldBe(HttpMethod.Get);
        }

        [Fact]
        public async Task RegisterTimer_ValidateRequest()
        {
            await using var client = TestClient.CreateForDaprHttpInterator();

            const string actorType = "ActorType_Test";
            const string actorId = "ActorId_Test";
            const string timerName = "TimerName";
            const string payload = "JsonData";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                await httpInteractor.RegisterTimerAsync(actorType, actorId, timerName, payload);
            });

            request.Dismiss();

            var actualPath = request.Request.RequestUri.LocalPath.TrimStart('/');
            var expectedPath = string.Format(CultureInfo.InvariantCulture, Constants.ActorTimerRelativeUrlFormat, actorType, actorId, timerName);

            actualPath.ShouldBe(expectedPath);
            request.Request.Method.ShouldBe(HttpMethod.Put);
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

            actualPath.ShouldBe(expectedPath);
            request.Request.Method.ShouldBe(HttpMethod.Delete);
        }

        [Fact]
        public async Task Call_WithApiTokenSet()
        {
            await using var client = TestClient.CreateForDaprHttpInterator(apiToken: "test_token");

            const string actorType = "ActorType_Test";
            const string actorId = "ActorId_Test";
            const string timerName = "TimerName";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                await httpInteractor.UnregisterTimerAsync(actorType, actorId, timerName);
            });

            request.Dismiss();

            request.Request.Headers.TryGetValues("dapr-api-token", out var headerValues);
            headerValues.Count().ShouldBe(1);
            headerValues.First().ShouldBe("test_token");
        }

        [Fact]
        public async Task Call_WithoutApiToken()
        {
            await using var client = TestClient.CreateForDaprHttpInterator();

            const string actorType = "ActorType_Test";
            const string actorId = "ActorId_Test";
            const string timerName = "TimerName";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                await httpInteractor.UnregisterTimerAsync(actorType, actorId, timerName);
            });

            request.Dismiss();

            request.Request.Headers.TryGetValues("dapr-api-token", out var headerValues);
            headerValues.ShouldBeNull();
        }

        [Fact]
        public async Task Call_ValidateUnsuccessfulResponse()
        {
            await using var client = TestClient.CreateForDaprHttpInterator();

            const string actorType = "ActorType_Test";
            const string actorId = "ActorId_Test";
            const string timerName = "TimerName";

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

            const string actorType = "ActorType_Test";
            const string actorId = "ActorId_Test";
            const string timerName = "TimerName";

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

            const string actorType = "ActorType_Test";
            const string actorId = "ActorId_Test";
            const string timerName = "TimerName";

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

            const string actorType = "ActorType_Test";
            const string actorId = "ActorId_Test";
            const string methodName = "MethodName";
            const string payload = "JsonData";

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

            const string actorType = "ActorType_Test";
            const string actorId = "ActorId_Test";
            const string methodName = "MethodName";
            const string payload = "JsonData";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                await httpInteractor.InvokeActorMethodWithoutRemotingAsync(actorType, actorId, methodName, payload);
            });

            request.Dismiss();
            Assert.False(request.Request.Headers.Contains(Constants.ReentrancyRequestHeaderName));
        }

        [Fact]
        public async Task GetState_TTLExpireTimeExists()
        {
            await using var client = TestClient.CreateForDaprHttpInterator();

            const string actorType = "ActorType_Test";
            const string actorId = "ActorId_Test";
            const string keyName = "StateKey_Test";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                return await httpInteractor.GetStateAsync(actorType, actorId, keyName);
            });

            var message = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("test"),
                Headers =
                {
                    { "Metadata.ttlExpireTime", "2023-04-05T23:22:21Z" },
                },
            };

            var actual = await request.CompleteAsync(message);
            Assert.Equal("test", actual.Value);
            var expTTL = new DateTimeOffset(2023, 04, 05, 23, 22, 21, 0, new GregorianCalendar(), new TimeSpan(0, 0, 0));
            Assert.Equal(expTTL, actual.TTLExpireTime);
        }

        [Fact]
        public async Task GetState_TTLExpireTimeNotExists()
        {
            await using var client = TestClient.CreateForDaprHttpInterator();

            const string actorType = "ActorType_Test";
            const string actorId = "ActorId_Test";
            const string keyName = "StateKey_Test";

            var request = await client.CaptureHttpRequestAsync(async httpInteractor =>
            {
                return await httpInteractor.GetStateAsync(actorType, actorId, keyName);
            });

            var message = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("test"),
            };

            var actual = await request.CompleteAsync(message);
            Assert.Equal("test", actual.Value);
            Assert.False(actual.TTLExpireTime.HasValue);
        }
    }
}
