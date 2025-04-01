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
using System.Text.Json;
using System.Threading.Tasks;
using Dapr.Actors.AspNetCore.IntegrationTest.App.ActivationTests;
using Xunit;
using Xunit.Sdk;

namespace Dapr.Actors.AspNetCore.IntegrationTest;

public class ActivationTests
{
    private readonly JsonSerializerOptions options = new JsonSerializerOptions()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public async Task CanActivateActorWithDependencyInjection()
    {
        using var factory = new AppWebApplicationFactory();
        var httpClient = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });

        // Doing this twice verifies that the Actor stays active and retains state using DI.
        var text = await IncrementCounterAsync(httpClient, "A");
        Assert.Equal("0", text);

        text = await IncrementCounterAsync(httpClient, "A");
        Assert.Equal("1", text);

        await DeactivateActor(httpClient, "A");
    }

    private async Task<string> IncrementCounterAsync(HttpClient httpClient, string actorId)
    {
        var actorTypeName = nameof(DependencyInjectionActor);
        var methodName = nameof(DependencyInjectionActor.IncrementAsync);

        var request = new HttpRequestMessage(HttpMethod.Put, $"http://localhost/actors/{actorTypeName}/{actorId}/method/{methodName}");
        var response = await httpClient.SendAsync(request);
        await Assert2XXStatusAsync(response);

        return await response.Content.ReadAsStringAsync();
    }

    private async Task DeactivateActor(HttpClient httpClient, string actorId)
    {
        var actorTypeName = nameof(DependencyInjectionActor);

        var request = new HttpRequestMessage(HttpMethod.Delete, $"http://localhost/actors/{actorTypeName}/{actorId}");
        var response = await httpClient.SendAsync(request);
        await Assert2XXStatusAsync(response);
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