// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.Dapr.AspNetCore.IntegrationTest
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.Dapr.AspNetCore.IntegrationTest.App;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class RoutingIntegrationTest
    {
        [TestMethod]
        public async Task StateClient_CanBindFromState()
        {
            using (var factory = new AppWebApplicationFactory())
            {
                var httpClient = factory.CreateClient();
                var stateClient = factory.StateClient;

                await stateClient.SaveStateAsync("test", new Widget() { Size = "small", Count = 17, });

                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/routingwithstateentry/test");
                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var widget = await stateClient.GetStateAsync<Widget>("test");
                widget.Count.Should().Be(18);
            }
        }
    }
}