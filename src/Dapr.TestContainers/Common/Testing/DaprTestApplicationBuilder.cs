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
using System.Threading.Tasks;
using Dapr.TestContainers.Common.Testing;
using Dapr.TestContainers.Harnesses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Dapr.E2E.Test.Common;

/// <summary>
/// Fluent builder for creating test applications with Dapr harnesses.
/// </summary>
public sealed class DaprTestApplicationBuilder(BaseHarness harness)
{
    private Action<WebApplicationBuilder>? _configureServices;
    private Action<WebApplication>? _configureApp;

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
    /// Builds and starts the test application and harness.
    /// </summary>
    /// <returns></returns>
    public async Task<DaprTestApplication> BuildAndStartAsync()
    {
        await harness.InitializeAsync();

        WebApplication? app = null;
        if (_configureServices is not null || _configureApp is not null)
        {
            // Set environment variables
            Environment.SetEnvironmentVariable("DAPR_HTTP_ENDPOINT", $"http://127.0.0.1:{harness.DaprHttpPort}");
            Environment.SetEnvironmentVariable("DAPR_GRPC_ENDPOINT", $"http://127.0.0.1:{harness.DaprGrpcPort}");

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Logging.AddSimpleConsole();
            builder.WebHost.UseUrls($"http://0.0.0.0:{harness.AppPort}");

            _configureServices?.Invoke(builder);

            app = builder.Build();
            _configureApp?.Invoke(app);

            await app.StartAsync();
        }

        return new DaprTestApplication(harness, app);
    }
}
