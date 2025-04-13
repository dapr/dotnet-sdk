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

using System;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Client;
using Shouldly;
using Xunit;

namespace Dapr.Actors.Test;

public class ApiTokenTests
{
    [Fact(Skip = "https://github.com/dapr/dotnet-sdk/issues/596")]
    public async Task CreateProxyWithRemoting_WithApiToken()
    {
        await using var client = TestClient.CreateForMessageHandler();

        var actorId = new ActorId("abc");
        var options = new ActorProxyOptions
        {
            DaprApiToken = "test_token",
        };

        var request = await client.CaptureHttpRequestAsync(async handler =>
        {
            var factory = new ActorProxyFactory(options, handler);
            var proxy = factory.CreateActorProxy<ITestActor>(actorId, "TestActor");
            await proxy.SetCountAsync(1, new CancellationToken());
        });

        request.Dismiss();

        var headerValues = request.Request.Headers.GetValues("dapr-api-token");
        headerValues.ShouldContain("test_token");
    }

    [Fact(Skip = "https://github.com/dapr/dotnet-sdk/issues/596")]
    public async Task CreateProxyWithRemoting_WithNoApiToken()
    {
        await using var client = TestClient.CreateForMessageHandler();

        var actorId = new ActorId("abc");

        var request = await client.CaptureHttpRequestAsync(async handler =>
        {
            var factory = new ActorProxyFactory(null, handler);
            var proxy = factory.CreateActorProxy<ITestActor>(actorId, "TestActor");
            await proxy.SetCountAsync(1, new CancellationToken());
        });

        request.Dismiss();

        Assert.Throws<InvalidOperationException>(() =>
        {
            request.Request.Headers.GetValues("dapr-api-token");
        });
    }

    [Fact]
    public async Task CreateProxyWithNoRemoting_WithApiToken()
    {
        await using var client = TestClient.CreateForMessageHandler();

        var actorId = new ActorId("abc");
        var options = new ActorProxyOptions
        {
            DaprApiToken = "test_token",
        };

        var request = await client.CaptureHttpRequestAsync(async handler =>
        {
            var factory = new ActorProxyFactory(options, handler);
            var proxy = factory.Create(actorId, "TestActor");
            await proxy.InvokeMethodAsync("SetCountAsync", 1, new CancellationToken());
        });

        request.Dismiss();

        var headerValues = request.Request.Headers.GetValues("dapr-api-token");
        headerValues.ShouldContain("test_token");
    }

    [Fact]
    public async Task CreateProxyWithNoRemoting_WithNoApiToken()
    {
        await using var client = TestClient.CreateForMessageHandler();

        var actorId = new ActorId("abc");

        var request = await client.CaptureHttpRequestAsync(async handler =>
        {
            var factory = new ActorProxyFactory(null, handler);
            var proxy = factory.Create(actorId, "TestActor");
            await proxy.InvokeMethodAsync("SetCountAsync", 1, new CancellationToken());
        });

        request.Dismiss();

        Assert.Throws<InvalidOperationException>(() =>
        {
            request.Request.Headers.GetValues("dapr-api-token");
        });
    }
}