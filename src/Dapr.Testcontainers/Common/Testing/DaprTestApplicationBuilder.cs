// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//  ------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapr.Testcontainers.Harnesses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Dapr.Testcontainers.Common.Testing;

/// <summary>
/// Fluent builder for creating test applications with Dapr harnesses.
/// </summary>
public sealed class DaprTestApplicationBuilder(BaseHarness harness)
{
    private Action<WebApplicationBuilder>? _configureServices;
    private Action<WebApplication>? _configureApp;
    private bool _shouldLoadResourcesFirst = true;

    /// <summary>
    /// Configures services for the test application.
    /// </summary>
    public DaprTestApplicationBuilder ConfigureServices(Action<WebApplicationBuilder> configure)
    {
        _configureServices = configure;
        return this;
    }

    /// <summary>
    /// Configures the test application pipeline.
    /// </summary>
    public DaprTestApplicationBuilder ConfigureApp(Action<WebApplication> configure)
    {
        _configureApp = configure;
        return this;
    }

    /// <summary>
    /// Configures the startup order of Dapr resources and the application.
    /// </summary>
    /// <param name="shouldLoadResourcesFirst">
    /// If true (default), Dapr container starts before the app. If false, the
    /// app starts before the Dapr container.
    /// </param>
    public DaprTestApplicationBuilder WithDaprStartupOrder(bool shouldLoadResourcesFirst)
    {
        _shouldLoadResourcesFirst = shouldLoadResourcesFirst;
        return this;
    }

    /// <summary>
    /// Builds and starts the test application and harness.
    /// </summary>
    /// <returns></returns>
    public async Task<DaprTestApplication> BuildAndStartAsync()
    {
        WebApplication? app = null;

        if (_shouldLoadResourcesFirst)
        {
            // Load the harness and resources, then the app
            await harness.InitializeAsync();

            if (_configureServices is not null || _configureApp is not null)
            {
                app = CreateApp();
                await app.StartAsync();
            }

            return new DaprTestApplication(harness, app);
        }
        
        // App-first: start app, then start resources
        // If daprd cannot bind the chosen ports, restart the app with new ports
        const int maxAttempts = 5;
        Exception? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            WebApplication? attemptApp = null;

            try
            {
                // Pre-assign prots for the app knows where Dapr will be (avoid collisions)
                var httpPort = PortUtilities.GetAvailablePort();
                var grpcPort = PortUtilities.GetAvailablePort();
                while (grpcPort == httpPort)
                    grpcPort = PortUtilities.GetAvailablePort();
                
                var appPort = PortUtilities.GetAvailablePort();
                while (appPort == httpPort || appPort == grpcPort)
                    appPort = PortUtilities.GetAvailablePort();

                harness.SetPorts(httpPort, grpcPort);
                harness.SetAppPort(appPort);

                // Load the app (configuration/services/pipeline), but delay StartAsync until daprd is up
                if (_configureServices is not null || _configureApp is not null)
                {
                    attemptApp = CreateApp();
                    await attemptApp.StartAsync();
                }

                await harness.InitializeAsync();

                return new DaprTestApplication(harness, attemptApp);
            }
            catch (Exception ex)
            {
                lastError = ex;

                if (attemptApp is not null)
                {
                    try
                    {
                        await attemptApp.StopAsync();
                    }
                    finally
                    {
                        await attemptApp.DisposeAsync();
                    }
                }
                
                // Try again with a frest set of ports
            }
        }

        throw new InvalidOperationException(
            $"Failed to start app-first Dapr test application after {maxAttempts} attempts.", lastError);
    }

    private WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        
        // Configure Dapr endpoints via in-memory configuration instead of environment variables
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "DAPR_HTTP_ENDPOINT", $"http://127.0.0.1:{harness.DaprHttpPort}" },
            { "DAPR_GRPC_ENDPOINT", $"http://127.0.0.1:{harness.DaprGrpcPort}" }
        });
        
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole();
        builder.WebHost.UseUrls($"http://0.0.0.0:{harness.AppPort}");
        
        _configureServices?.Invoke(builder);

        var app = builder.Build();
            
        _configureApp?.Invoke(app);

        return app;
    }
}
