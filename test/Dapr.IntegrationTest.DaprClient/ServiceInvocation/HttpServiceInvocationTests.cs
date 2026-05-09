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

using System.Net.Http.Json;
using Dapr.Client;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Harnesses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

namespace Dapr.IntegrationTest.DaprClient.ServiceInvocation;

/// <summary>
/// Integration tests for HTTP-based service invocation via <see cref="global::Dapr.Client.DaprClient"/>.
/// Covers:
/// <list type="bullet">
///   <item><description><c>DaprClient.CreateInvokeHttpClient</c> – static factory that returns a reusable
///   <see cref="System.Net.Http.HttpClient"/> whose handler transparently rewrites requests through the
///   Dapr sidecar.</description></item>
///   <item><description><c>DaprClient.CreateInvokableHttpClient</c> – instance method equivalent.</description></item>
///   <item><description><c>DaprClient.InvokeMethodAsync</c> overloads – typed request/response, no-body
///   request, and void-return overloads.</description></item>
/// </list>
/// </summary>
public sealed class HttpServiceInvocationTests
{
    // -----------------------------------------------------------------------
    // Shared model types for the minimal test app endpoints.
    // -----------------------------------------------------------------------

    private sealed record Transaction(string Id, decimal Amount);
    private sealed record Account(string Id, decimal Balance);
    private sealed record Greeting(string Message);

    // -----------------------------------------------------------------------
    // CreateInvokeHttpClient (static) tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <see cref="global::Dapr.Client.DaprClient.CreateInvokeHttpClient"/> produces an
    /// <see cref="System.Net.Http.HttpClient"/> that successfully routes a POST request with a typed payload
    /// through the sidecar and returns a deserialized response.
    /// </summary>
    [Fact]
    public async Task CreateInvokeHttpClient_PostWithTypedPayload_ReturnsExpectedResponse()
    {
        await using var ctx = await CreateTestContextAsync();

        using var httpClient = global::Dapr.Client.DaprClient.CreateInvokeHttpClient(
            appId: ctx.AppId,
            daprEndpoint: ctx.DaprHttpEndpoint);

        var response = await httpClient.PostAsJsonAsync(
            "/accountDetails",
            new Transaction("42", 100m),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var account = await response.Content.ReadFromJsonAsync<Account>(TestContext.Current.CancellationToken);

        Assert.NotNull(account);
        Assert.Equal("42", account.Id);
        Assert.Equal(200m, account.Balance);
    }

    /// <summary>
    /// Verifies that a GET request via <see cref="global::Dapr.Client.DaprClient.CreateInvokeHttpClient"/>
    /// routes correctly and returns the expected response.
    /// </summary>
    [Fact]
    public async Task CreateInvokeHttpClient_GetRequest_ReturnsExpectedResponse()
    {
        await using var ctx = await CreateTestContextAsync();

        using var httpClient = global::Dapr.Client.DaprClient.CreateInvokeHttpClient(
            appId: ctx.AppId,
            daprEndpoint: ctx.DaprHttpEndpoint);

        var response = await httpClient.GetAsync("/greeting", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var greeting = await response.Content.ReadFromJsonAsync<Greeting>(TestContext.Current.CancellationToken);

        Assert.NotNull(greeting);
        Assert.Equal("Hello from the test app!", greeting.Message);
    }

    // -----------------------------------------------------------------------
    // CreateInvokableHttpClient (instance) tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that the instance method <see cref="global::Dapr.Client.DaprClient.CreateInvokableHttpClient"/>
    /// returns a client pre-configured with the given <c>app-id</c> as its base address so that
    /// relative URIs can be used directly.
    /// </summary>
    [Fact]
    public async Task CreateInvokableHttpClient_WithAppId_RelativeUriRoutesThroughSidecar()
    {
        await using var ctx = await CreateTestContextAsync();

        using var daprClient = new DaprClientBuilder()
            .UseHttpEndpoint(ctx.DaprHttpEndpoint)
            .UseGrpcEndpoint(ctx.DaprGrpcEndpoint)
            .Build();

        // CreateInvokableHttpClient is an instance method that creates a client pre-configured
        // to route through this client's sidecar.
        using var httpClient = daprClient.CreateInvokableHttpClient(appId: ctx.AppId);

        // With appId set as base address, a relative URI is enough.
        var response = await httpClient.GetAsync("/greeting", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var greeting = await response.Content.ReadFromJsonAsync<Greeting>(TestContext.Current.CancellationToken);

        Assert.NotNull(greeting);
        Assert.Equal("Hello from the test app!", greeting.Message);
    }

    // -----------------------------------------------------------------------
    // InvokeMethodAsync overloads (DaprClient instance)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies the <c>InvokeMethodAsync&lt;TRequest, TResponse&gt;(appId, method, data)</c> overload:
    /// sends a typed request body and deserializes a typed response.
    /// </summary>
    [Fact]
    [System.Obsolete]
    public async Task InvokeMethodAsync_TypedRequestAndResponse_ReturnsExpectedResponse()
    {
        await using var ctx = await CreateTestContextAsync();

        using var daprClient = new DaprClientBuilder()
            .UseHttpEndpoint(ctx.DaprHttpEndpoint)
            .UseGrpcEndpoint(ctx.DaprGrpcEndpoint)
            .Build();

        var account = await daprClient.InvokeMethodAsync<Transaction, Account>(
            appId: ctx.AppId,
            methodName: "accountDetails",
            data: new Transaction("99", 50m),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(account);
        Assert.Equal("99", account.Id);
        Assert.Equal(150m, account.Balance);
    }

    /// <summary>
    /// Verifies the <c>InvokeMethodAsync&lt;TResponse&gt;(appId, method)</c> overload:
    /// sends no request body and deserializes a typed response.
    /// </summary>
    [Fact]
    [System.Obsolete]
    public async Task InvokeMethodAsync_NoRequestBody_ReturnsTypedResponse()
    {
        await using var ctx = await CreateTestContextAsync();

        using var daprClient = new DaprClientBuilder()
            .UseHttpEndpoint(ctx.DaprHttpEndpoint)
            .UseGrpcEndpoint(ctx.DaprGrpcEndpoint)
            .Build();

        // GET /greeting has no request body.
        var greeting = await daprClient.InvokeMethodAsync<Greeting>(
            System.Net.Http.HttpMethod.Get,
            appId: ctx.AppId,
            methodName: "greeting",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(greeting);
        Assert.Equal("Hello from the test app!", greeting.Message);
    }

    /// <summary>
    /// Verifies the <c>InvokeMethodAsync&lt;TRequest&gt;(appId, method, data)</c> overload:
    /// sends a typed request body and expects no response body (void return).
    /// </summary>
    [Fact]
    [System.Obsolete]
    public async Task InvokeMethodAsync_TypedRequestVoidResponse_DoesNotThrow()
    {
        await using var ctx = await CreateTestContextAsync();

        using var daprClient = new DaprClientBuilder()
            .UseHttpEndpoint(ctx.DaprHttpEndpoint)
            .UseGrpcEndpoint(ctx.DaprGrpcEndpoint)
            .Build();

        // POST /notify accepts a payload and returns 204 No Content.
        await daprClient.InvokeMethodAsync<Transaction>(
            appId: ctx.AppId,
            methodName: "notify",
            data: new Transaction("1", 10m),
            cancellationToken: TestContext.Current.CancellationToken);

        // If we get here without an exception the test passes.
    }

    // -----------------------------------------------------------------------
    // Shared test-app factory
    // -----------------------------------------------------------------------

    private sealed record HarnessContext(
        string AppId,
        string DaprHttpEndpoint,
        string DaprGrpcEndpoint,
        IAsyncDisposable Inner) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await Inner.DisposeAsync();
    }

    private static async Task<HarnessContext> CreateTestContextAsync()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("dapr-client-http");
        var harness = new DaprHarnessBuilder(componentsDir).BuildServiceInvocation();

        var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureApp(app =>
            {
                // POST /accountDetails → returns Account with Balance = Amount + 100
                app.MapPost("/accountDetails", ([FromBody] Transaction t) =>
                    Results.Ok(new Account(t.Id, t.Amount + 100m)));

                // GET /greeting → returns a fixed greeting
                app.MapGet("/greeting", () =>
                    Results.Ok(new Greeting("Hello from the test app!")));

                // POST /notify → accepts a transaction, returns 204
                app.MapPost("/notify", ([FromBody] Transaction _) =>
                    Results.NoContent());
            })
            .BuildAndStartAsync();

        return new HarnessContext(
            harness.AppId,
            $"http://127.0.0.1:{harness.DaprHttpPort}",
            $"http://127.0.0.1:{harness.DaprGrpcPort}",
            testApp);
    }
}
