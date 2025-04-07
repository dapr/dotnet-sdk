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
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Sdk;

namespace Dapr.Actors.AspNetCore.IntegrationTest;

public class HostingTests
{
    [Fact]
    public void MapActorsHandlers_WithoutAddActors_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            // Initializes web pipeline which will trigger the exception we throw.
            //
            // NOTE: in 3.1 TestServer.CreateClient triggers the failure, in 5.0 it's Host.Start
            using var host = CreateHost<BadStartup>();
            var server = host.GetTestServer();
            server.CreateClient();
        });

        Assert.Equal(
            "The ActorRuntime service is not registered with the dependency injection container. " +
            "Call AddActors() inside ConfigureServices() to register the actor runtime and actor types.",
            exception.Message);
    }

    [Fact]
    public async Task MapActorsHandlers_IncludesHealthChecks()
    {
        using var factory = new AppWebApplicationFactory();

        var httpClient = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
        var response = await httpClient.GetAsync("/healthz");
        await Assert2XXStatusAsync(response);
    }

    [Fact]
    public async Task ActorsHealthz_ShouldNotRequireAuthorization()
    {
        using var host = CreateHost<AuthorizedRoutesStartup>();
        var server = host.GetTestServer();

        var httpClient = server.CreateClient();
        var response = await httpClient.GetAsync("/healthz");
        await Assert2XXStatusAsync(response);
    }

    // We add our own health check on /healthz with worse priority than one
    // that would be added by a user. Make sure this works and the if the user
    // adds their own health check it will win.
    [Fact]
    public async Task MapActorsHandlers_ActorHealthCheckDoesNotConflict()
    {
        using var host = CreateHost<HealthCheckStartup>();
        var server = host.GetTestServer();

        var httpClient = server.CreateClient();
        var response = await httpClient.GetAsync("/healthz");
        await Assert2XXStatusAsync(response);

        var text = await response.Content.ReadAsStringAsync();
        Assert.Equal("Ice Cold, Solid Gold!", text);
    }

    // Regression test for #434
    [Fact]
    public async Task MapActorsHandlers_WorksWithFallbackRoute()
    {
        using var host = CreateHost<FallbackRouteStartup>();
        var server = host.GetTestServer();

        var httpClient = server.CreateClient();
        var response = await httpClient.GetAsync("/dapr/config");
        await Assert2XXStatusAsync(response);
    }

    private static IHost CreateHost<TStartup>() where TStartup : class
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

    private class BadStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            // no call to AddActors here. That's bad!
            services.AddRouting();
            services.AddHealthChecks();
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

    private class AuthorizedRoutesStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddActors(default);
            services.AddAuthentication().AddDapr(options => options.Token = "abcdefg");

            services.AddAuthorization(o => o.AddDapr());
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapActorsHandlers().RequireAuthorization("Dapr");
            });
        }
    }

    private class FallbackRouteStartup
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
                // This routing feature registers a "route of last resort" which is what
                // was tripping out Actors prior to changing how they are registered.
                endpoints.MapFallback(context =>
                {
                    throw new InvalidTimeZoneException("This should not be called!");
                });
                endpoints.MapActorsHandlers();
            });
        }
    }

    private class HealthCheckStartup
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
                endpoints.MapHealthChecks("/healthz", new HealthCheckOptions()
                {
                    // Write something different so we know this one is called.
                    ResponseWriter = async (httpContext, report) =>
                    {
                        await httpContext.Response.WriteAsync(
                            report.Status == HealthStatus.Healthy ?
                                "Ice Cold, Solid Gold!" :
                                "Oh Noes!");
                    },
                });
                endpoints.MapActorsHandlers();
            });
        }
    }

    private async Task Assert2XXStatusAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.Content == null)
        {
            throw new XunitException($"The response failed with a {response.StatusCode} and no body.");
        }

        // We assume a textual response. #YOLO
        var text = await response.Content.ReadAsStringAsync();
        throw new XunitException($"The response failed with a {response.StatusCode} and body:" + Environment.NewLine + text);
    }
}