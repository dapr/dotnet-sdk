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
using Dapr.TestContainers.Harnesses;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.TestContainers.Common.Testing;

/// <summary>
/// Represents a running Dapr test application.
/// </summary>
public sealed class DaprTestApplication : IAsyncDisposable
{
    private readonly BaseHarness _harness;
    private readonly WebApplication? _app;

    internal DaprTestApplication(BaseHarness harness, WebApplication? app)
    {
        _harness = harness;
        _app = app;
    }

    /// <summary>
    /// Gets a service from the application's DI container.
    /// </summary>
    public T GetRequiredService<T>() where T : notnull
    {
        if (_app is null)
            throw new InvalidOperationException("No app configured");
        
        return _app.Services.GetRequiredService<T>();
    }
    
    /// <summary>
    /// Creates a service scope.
    /// </summary>
    public IServiceScope CreateScope() =>
        _app?.Services.CreateScope() ?? throw new InvalidOperationException("No app configured");

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Environment.SetEnvironmentVariable("DAPR_HTTP_ENDPOINT", null);
        Environment.SetEnvironmentVariable("DAPR_GRPC_ENDPOINT", null);

        if (_app is not null)
            await _app.DisposeAsync();

        await _harness.DisposeAsync();
    }
}
