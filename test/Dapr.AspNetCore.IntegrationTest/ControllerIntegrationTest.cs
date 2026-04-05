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

using System.Text.Json;
using System.Net.Http;
using System.Threading.Tasks;
using Dapr.AspNetCore.IntegrationTest.App;
using Shouldly;
using Xunit;

namespace Dapr.AspNetCore.IntegrationTest;

public class ControllerIntegrationTest
{
    [Fact]
    public async Task ModelBinder_CanBindFromState()
    {
        await using var factory = new AppWebApplicationFactory();
        var httpClient = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
        var daprClient = factory.DaprClient;

        await daprClient.SaveStateAsync("testStore", "test", new Widget() { Size = "small", Count = 17, }, cancellationToken: TestContext.Current.CancellationToken);

        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/controllerwithoutstateentry/test");
        var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var widget = await daprClient.GetStateAsync<Widget>("testStore", "test", cancellationToken: TestContext.Current.CancellationToken);
        widget.Count.ShouldBe(18);
    }

    [Fact]
    public async Task ModelBinder_GetFromStateEntryWithKeyPresentInStateStore_ReturnsStateValue()
    {
        await using var factory = new AppWebApplicationFactory();
        var httpClient = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
        var daprClient = factory.DaprClient;

        var widget = new Widget { Size = "small", Count = 17, };
        await daprClient.SaveStateAsync("testStore", "test", widget, cancellationToken: TestContext.Current.CancellationToken);
        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/controllerwithoutstateentry/test");
        var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var responseWidget = JsonSerializer.Deserialize<Widget>(responseContent);
        responseWidget.Size.ShouldBe(widget.Size);
        responseWidget.Count.ShouldBe(widget.Count);
    }

    [Fact]
    public async Task ModelBinder_GetFromStateEntryWithKeyNotInStateStore_ReturnsNull()
    {
        await using var factory = new AppWebApplicationFactory();
        var httpClient = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/controllerwithoutstateentry/test");
        var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var responseWidget =
            !string.IsNullOrWhiteSpace(responseContent) ? JsonSerializer.Deserialize<Widget>(responseContent) : null;
        Assert.Null(responseWidget);
    }

    [Fact]
    public async Task ModelBinder_CanBindFromState_WithStateEntry()
    {
        await using var factory = new AppWebApplicationFactory();
        var httpClient = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
        var daprClient = factory.DaprClient;

        await daprClient.SaveStateAsync("testStore", "test", new Widget() { Size = "small", Count = 17, }, cancellationToken: TestContext.Current.CancellationToken);

        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/controllerwithstateentry/test");
        var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var widget = await daprClient.GetStateAsync<Widget>("testStore", "test", cancellationToken: TestContext.Current.CancellationToken);
        widget.Count.ShouldBe(18);
    }

    [Fact]
    public async Task ModelBinder_CanBindFromState_WithStateEntryAndCustomKey()
    {
        await using var factory = new AppWebApplicationFactory();
        var httpClient = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
        var daprClient = factory.DaprClient;

        await daprClient.SaveStateAsync("testStore", "test", new Widget() { Size = "small", Count = 17, }, cancellationToken: TestContext.Current.CancellationToken);

        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/controllerwithstateentryandcustomkey/test");
        var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var widget = await daprClient.GetStateAsync<Widget>("testStore", "test", cancellationToken: TestContext.Current.CancellationToken);
        widget.Count.ShouldBe(18);
    }

    [Fact]
    public async Task ModelBinder_GetFromStateEntryWithStateEntry_WithKeyPresentInStateStore()
    {
        await using var factory = new AppWebApplicationFactory();
        var httpClient = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
        var daprClient = factory.DaprClient;

        var widget = new Widget() { Size = "small", Count = 17, };
        await daprClient.SaveStateAsync("testStore", "test", widget, cancellationToken: TestContext.Current.CancellationToken);
        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/controllerwithstateentry/test");
        var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var responseWidget = JsonSerializer.Deserialize<Widget>(responseContent);
        responseWidget.Size.ShouldBe(widget.Size);
        responseWidget.Count.ShouldBe(widget.Count);
    }

    [Fact]
    public async Task ModelBinder_GetFromStateEntryWithStateEntry_WithKeyNotInStateStore()
    {
        await using var factory = new AppWebApplicationFactory();
        var httpClient = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });

        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/controllerwithstateentry/test");
        var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var responseWidget = !string.IsNullOrWhiteSpace(responseContent) ? JsonSerializer.Deserialize<Widget>(responseContent) : null;
        Assert.Null(responseWidget);
    }

    [Fact]
    public async Task ModelBinder_CanGetOutOfTheWayWhenTheresNoBinding()
    {
        await using var factory = new AppWebApplicationFactory();
        var httpClient = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });

        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/echo-user?name=jimmy");
        var response = await httpClient.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
