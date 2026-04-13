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
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.IntegrationTest.Actors.ExceptionTesting;
using Dapr.IntegrationTest.Actors.Infrastructure;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Harnesses;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Actors;

/// <summary>
/// Integration tests that verify Dapr actor exception propagation back to the caller.
/// </summary>
public sealed class ExceptionTests
{
    /// <summary>
    /// Verifies that an <see cref="ActorMethodInvocationException"/> is raised on the client
    /// when the actor method throws, and that the exception message includes diagnostic details.
    /// </summary>
    [Fact]
    public async Task ActorCanProvideExceptionDetails()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var proxy = proxyFactory.CreateActorProxy<IExceptionActor>(ActorId.CreateRandom(), "ExceptionActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        var ex = await Assert.ThrowsAsync<ActorMethodInvocationException>(
            () => proxy.ExceptionExample());

        Assert.Contains("Remote Actor Method Exception", ex.Message);
        Assert.Contains("ExceptionExample", ex.Message);
    }

    // ------------------------------------------------------------------
    // Test infrastructure helpers
    // ------------------------------------------------------------------

    private static async Task<Dapr.Testcontainers.Common.Testing.DaprTestApplication> CreateTestAppAsync(
        CancellationToken cancellationToken)
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("actor-exception-components");

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(
            needsActorState: true,
            cancellationToken: cancellationToken);
        await environment.StartAsync(cancellationToken);

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildActors();

        return await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddActors(options =>
                {
                    options.Actors.RegisterActor<ExceptionActor>();
                });
            })
            .ConfigureApp(app =>
            {
                app.MapActorsHandlers();
            })
            .BuildAndStartAsync();
    }
}
