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

    public class ControllerIntegrationTest
    {
        [Fact]
        public async Task ModelBinder_CanBindFromState()
        {
            using (var factory = new AppWebApplicationFactory())
            {
                var httpClient = factory.CreateClient();
                var stateClient = factory.StateClient;

                await stateClient.SaveStateAsync("testStore", "test", new Widget() { Size = "small", Count = 17, });

                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/controllerwithoutstateentry/test");
                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var widget = await stateClient.GetStateAsync<Widget>("testStore", "test");
                widget.Count.Should().Be(18);
            }
        }

        [Fact]
        public async Task ModelBinder_CanBindFromState_WithStateEntry()
        {
            using (var factory = new AppWebApplicationFactory())
            {
                var httpClient = factory.CreateClient();
                var stateClient = factory.StateClient;

                await stateClient.SaveStateAsync("testStore", "test", new Widget() { Size = "small", Count = 17, });

                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/controllerwithstateentry/test");
                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var widget = await stateClient.GetStateAsync<Widget>("testStore", "test");
                widget.Count.Should().Be(18);
            }
        }

        [Fact]
        public async Task ModelBinder_CanBindFromState_WithStateEntryAndCustomKey()
        {
            using (var factory = new AppWebApplicationFactory())
            {
                var httpClient = factory.CreateClient();
                var stateClient = factory.StateClient;

                await stateClient.SaveStateAsync("testStore", "test", new Widget() { Size = "small", Count = 17, });

                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/controllerwithstateentryandcustomkey/test");
                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var widget = await stateClient.GetStateAsync<Widget>("testStore", "test");
                widget.Count.Should().Be(18);
            }
        }
    }
}
