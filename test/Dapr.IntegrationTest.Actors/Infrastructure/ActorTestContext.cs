// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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
using Dapr.Testcontainers.Common.Testing;
using Dapr.Testcontainers.Harnesses;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Actors.Infrastructure;

/// <summary>
/// Combines a <see cref="DaprTestApplication"/> with its owning
/// <see cref="DaprTestEnvironment"/> so that both are disposed together
/// when the test ends. The <see cref="DaprTestEnvironment"/> must outlive
/// the application (placement / scheduler must stay up while the test runs).
/// </summary>
public sealed class ActorTestContext : IAsyncDisposable
{
    private readonly DaprTestEnvironment _environment;
    private readonly DaprTestApplication _app;

    /// <summary>
    /// Initializes a new <see cref="ActorTestContext"/>.
    /// </summary>
    internal ActorTestContext(DaprTestEnvironment environment, DaprTestApplication app)
    {
        _environment = environment;
        _app = app;
    }

    /// <summary>
    /// Creates a DI service scope from the running test application.
    /// </summary>
    public IServiceScope CreateScope() => _app.CreateScope();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Dispose the application (and its harness) before shutting down the environment
        // so the Dapr sidecar can drain cleanly before placement/scheduler stop.
        await _app.DisposeAsync();
        await _environment.DisposeAsync();
    }
}
