// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.AspNetCore.IntegrationTest
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using Dapr.AspNetCore.IntegrationTest.App;
    using FluentAssertions;
    using Xunit;

    public class RoutingIntegrationTest
    {
        [Fact]
        public async Task StateClient_CanBindFromState()
        {
            using (var factory = new AppWebApplicationFactory())
            {
                var httpClient = factory.CreateClient();
                var daprClient = factory.DaprClient;

                await daprClient.SaveStateAsync("testStore", "test", new Widget() { Size = "small", Count = 17, });

                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/routingwithstateentry/test");
                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var widget = await daprClient.GetStateAsync<Widget>("testStore", "test");
                widget.Count.Should().Be(18);
            }
        }
    }
}
