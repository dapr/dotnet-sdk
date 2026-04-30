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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dapr.Actors.Runtime;

public class ActorsEndpointRouteBuilderExtensionsTests
{
    [Fact]
    public async Task MapActorsHandlers_MapDaprConfigEndpoint()
    {
        using var host = CreateHost<ActorsStartup>(options =>
        {
            options.Actors.RegisterActor<TestActor>();
        });
        var server = host.GetTestServer();

        var httpClient = server.CreateClient();
        var response = await httpClient.GetAsync("/dapr/config", TestContext.Current.CancellationToken);

        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(@"{""entities"":[""TestActor""],""reentrancy"":{""enabled"":false}}", text);
    }

    [Fact]
    public async Task MapActorsHandlers_HealthzEndpointResponds()
    {
        using var host = CreateHost<ActorsStartup>(options =>
        {
            options.Actors.RegisterActor<TestActor>();
        });
        var server = host.GetTestServer();
        var httpClient = server.CreateClient();

        var response = await httpClient.GetAsync("/healthz", TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, $"Expected 2xx but got {response.StatusCode}");
    }

    [Fact]
    public async Task MapActorsHandlers_InvokeMethodRouteReturnsForRegisteredActor()
    {
        using var host = CreateHost<ActorsStartup>(options =>
        {
            options.Actors.RegisterActor<RealMethodActor>();
        });
        var server = host.GetTestServer();
        var httpClient = server.CreateClient();

        // PUT /actors/{actorTypeName}/{actorId}/method/{methodName}
        var request = new System.Net.Http.HttpRequestMessage(
            System.Net.Http.HttpMethod.Put,
            $"/actors/RealMethodActor/actor1/method/{nameof(IRealMethodActor.PingAsync)}");
        var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);

        // Not a 404 — route was matched and the method was invoked.
        Assert.NotEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(response.IsSuccessStatusCode, $"Expected 2xx but got {response.StatusCode}");
    }

    [Fact]
    public async Task MapActorsHandlers_UnregisteredActorType_ThrowsInvalidOperationException()
    {
        using var host = CreateHost<ActorsStartup>(options =>
        {
            options.Actors.RegisterActor<TestActor>();
        });
        var server = host.GetTestServer();
        var httpClient = server.CreateClient();

        // PUT /actors/{unknownType}/{id}/method/{method} — should throw because the type is not registered.
        var request = new System.Net.Http.HttpRequestMessage(
            System.Net.Http.HttpMethod.Put,
            "/actors/DoesNotExist/id1/method/Foo");

        // The TestServer propagates the unhandled exception from the route handler.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => httpClient.SendAsync(request, TestContext.Current.CancellationToken));
    }

    private static IHost CreateHost<TStartup>(Action<ActorRuntimeOptions> configure) where TStartup : class
    {
        var builder = Host
            .CreateDefaultBuilder()
            .ConfigureLogging(b =>
            {
                // shhhh
                b.SetMinimumLevel(LogLevel.None);
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<TStartup>();
                webBuilder.UseTestServer();
            })
            .ConfigureServices(services =>
            {
                services.AddActors(configure);
            });
        var host = builder.Build();
        try
        {
            host.Start();
        }
        catch
        {
            host.Dispose();
            throw;
        }

        return host;
    }

    private class ActorsStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddActors(default);
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapActorsHandlers();
            });
        }
    }

    private interface ITestActor : IActor
    {
    }

    private class TestActor : Actor, ITestActor
    {
        public TestActor(ActorHost host) : base(host) { }
    }

    private interface IRealMethodActor : IActor
    {
        Task PingAsync();
    }

    private class RealMethodActor : Actor, IRealMethodActor
    {
        public RealMethodActor(ActorHost host) : base(host) { }
        public Task PingAsync() => Task.CompletedTask;
    }
}
