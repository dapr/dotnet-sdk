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
        var response = await httpClient.GetAsync("/dapr/config");

        var text = await response.Content.ReadAsStringAsync();
        Assert.Equal(@"{""entities"":[""TestActor""],""reentrancy"":{""enabled"":false}}", text);
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
}