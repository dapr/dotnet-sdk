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

using System.Net.Http.Json;
using Dapr.Client;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Common.Testing;
using Dapr.Testcontainers.Harnesses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace Dapr.IntegrationTest.ServiceInvocation;

public sealed class ServiceInvocationTests
{
    [Fact]
    public async Task HttpInvocationReturnsApplicationResponse()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var context = await CreateHttpAppAsync(requireApiToken: false, cts.Token);

        using var client = DaprClient.CreateInvokeHttpClient(
            appId: context.AppId,
            daprEndpoint: context.Harness.DaprHttpEndpoint);
        var response = await client.PostAsJsonAsync(
            "/accountDetails",
            new Transaction("1", 50),
            cts.Token);

        response.EnsureSuccessStatusCode();
        var account = await response.Content.ReadFromJsonAsync<Account>(cancellationToken: cts.Token);
        Assert.NotNull(account);
        Assert.Equal("1", account.Id);
        Assert.Equal(150, account.Balance);
    }

    [Fact]
    public async Task HttpInvocationSuppliesConfiguredApplicationApiToken()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await using var context = await CreateHttpAppAsync(requireApiToken: true, cts.Token);

        using var client = DaprClient.CreateInvokeHttpClient(
            appId: context.AppId,
            daprEndpoint: context.Harness.DaprHttpEndpoint);
        var response = await client.PostAsJsonAsync(
            "/accountDetails-requires-api-token",
            new Transaction("1", 50),
            cts.Token);

        response.EnsureSuccessStatusCode();
        var account = await response.Content.ReadFromJsonAsync<Account>(cancellationToken: cts.Token);
        Assert.NotNull(account);
        Assert.Equal("1", account.Id);
        Assert.Equal(150, account.Balance);
    }

    private static async Task<ServiceInvocationContext> CreateHttpAppAsync(
        bool requireApiToken,
        CancellationToken cancellationToken)
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("service-invocation-components");
        var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync(cancellationToken: cancellationToken);
        await environment.StartAsync(cancellationToken);

        var appId = $"http-invocation-{Guid.NewGuid():N}";
        var options = new DaprRuntimeOptions()
            .WithAppId(appId);
        if (requireApiToken)
            options.WithEnvironmentVariable("APP_API_TOKEN", "abcdefg");

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .WithOptions(options)
            .BuildServiceInvocation();

        var app = await DaprHarnessBuilder.ForHarness(harness)
            .WithDaprStartupOrder(false)
            .ConfigureServices(builder =>
            {
                builder.Services.AddAuthentication().AddDapr(authentication =>
                    authentication.Token = "abcdefg");
                builder.Services.AddAuthorization(authorization => authorization.AddDapr());
            })
            .ConfigureApp(webApp =>
            {
                webApp.UseAuthentication();
                webApp.UseAuthorization();
                webApp.MapPost("/accountDetails", (Transaction transaction) =>
                    new Account(transaction.Id, transaction.Amount + 100));
                webApp.MapPost("/accountDetails-requires-api-token", [Authorize("Dapr")](Transaction transaction) =>
                    new Account(transaction.Id, transaction.Amount + 100));
            })
            .BuildAndStartAsync();

        return new ServiceInvocationContext(environment, harness, app, appId);
    }

    private sealed record Transaction(string Id, decimal Amount);

    private sealed record Account(string Id, decimal Balance);
}

internal sealed class ServiceInvocationContext(
    DaprTestEnvironment environment,
    ServiceInvocationHarness harness,
    DaprTestApplication application,
    string appId) : IAsyncDisposable
{
    public ServiceInvocationHarness Harness { get; } = harness;
    public string AppId { get; } = appId;

    public async ValueTask DisposeAsync()
    {
        await application.DisposeAsync();
        await environment.DisposeAsync();
    }
}
