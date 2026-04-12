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
// ------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;
using Dapr.IntegrationTest.Actors.Infrastructure;
using Dapr.IntegrationTest.Actors.Serialization;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Harnesses;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.Actors;

/// <summary>
/// Integration tests that verify custom JSON serialization when invoking actor methods via remoting.
/// </summary>
public sealed class SerializationTests
{
    /// <summary>
    /// Verifies that a complex payload — including extension data and a nested JSON element — survives
    /// a full actor remoting round-trip when custom JSON serializer options are configured.
    /// </summary>
    [Fact]
    public async Task ActorCanSupportCustomSerializer()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(useJsonSerialization: true, cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var proxy = proxyFactory.CreateActorProxy<ISerializationActor>(ActorId.CreateRandom(), "SerializationActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        var payload = new SerializationPayload("hello world")
        {
            Value = JsonSerializer.SerializeToElement(new { foo = "bar" }),
            ExtensionData = new Dictionary<string, object>
            {
                { "baz", "qux" },
                { "count", 42 },
            }
        };

        var result = await proxy.SendAsync("test", payload, cts.Token);

        Assert.Equal(payload.Message, result.Message);
        Assert.Equal(payload.Value.GetRawText(), result.Value.GetRawText());
        Assert.NotNull(result.ExtensionData);
        Assert.Equal(payload.ExtensionData!.Count, result.ExtensionData!.Count);

        foreach (var kvp in payload.ExtensionData)
        {
            Assert.True(result.ExtensionData.TryGetValue(kvp.Key, out var value));
            Assert.Equal(JsonSerializer.Serialize(kvp.Value), JsonSerializer.Serialize(value));
        }
    }

    /// <summary>
    /// Verifies that an actor interface with more than one method can dispatch each method
    /// independently when custom JSON serialization is active.
    /// </summary>
    [Fact]
    public async Task ActorCanSupportCustomSerializerAndCallMoreThanOneDefinedMethod()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var testApp = await CreateTestAppAsync(useJsonSerialization: true, cts.Token);

        using var scope = testApp.CreateScope();
        var proxyFactory = scope.ServiceProvider.GetRequiredService<IActorProxyFactory>();
        var proxy = proxyFactory.CreateActorProxy<ISerializationActor>(ActorId.CreateRandom(), "SerializationActor");

        await ActorRuntimeHelper.WaitForActorRuntimeAsync(proxy, cts.Token);

        var payload = DateTime.MinValue;
        var result = await proxy.AnotherMethod(payload);

        Assert.Equal(payload, result);
    }

    // ------------------------------------------------------------------
    // Test infrastructure helpers
    // ------------------------------------------------------------------

    private static async Task<Dapr.Testcontainers.Common.Testing.DaprTestApplication> CreateTestAppAsync(
        bool useJsonSerialization,
        CancellationToken cancellationToken)
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("actor-serialization-components");

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
                    options.UseJsonSerialization = useJsonSerialization;
                    if (useJsonSerialization)
                    {
                        options.JsonSerializerOptions = new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                            PropertyNameCaseInsensitive = true,
                            WriteIndented = true,
                        };
                    }
                    options.Actors.RegisterActor<SerializationActor>();
                });
            })
            .ConfigureApp(app =>
            {
                app.MapActorsHandlers();
            })
            .BuildAndStartAsync();
    }
}
