// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Dapr.AspNetCore.IntegrationTest
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.Dapr.AspNetCore.IntegrationTest.App;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ControllerIntegrationTest
    {
        [TestMethod]
        public async Task ModelBinder_CanBindFromState()
        {
            using (var factory = new AppWebApplicationFactory())
            {
                var httpClient = factory.CreateClient();
                var stateClient = factory.StateClient;

                await stateClient.SaveStateAsync("test", new Widget() { Size = "small", Count = 17, });

                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/controllerwithoutstateentry/test");
                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var widget = await stateClient.GetStateAsync<Widget>("test");
                widget.Count.Should().Be(18);
            }
        }

        [TestMethod]
        public async Task ModelBinder_CanBindFromState_WithStateEntry()
        {
            using (var factory = new AppWebApplicationFactory())
            {
                var httpClient = factory.CreateClient();
                var stateClient = factory.StateClient;

                await stateClient.SaveStateAsync("test", new Widget() { Size = "small", Count = 17, });

                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/controllerwithstateentry/test");
                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var widget = await stateClient.GetStateAsync<Widget>("test");
                widget.Count.Should().Be(18);
            }
        }

        [TestMethod]
        public async Task ModelBinder_CanBindFromState_WithStateEntryAndCustomKey()
        {
            using (var factory = new AppWebApplicationFactory())
            {
                var httpClient = factory.CreateClient();
                var stateClient = factory.StateClient;

                await stateClient.SaveStateAsync("test", new Widget() { Size = "small", Count = 17, });

                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/controllerwithstateentryandcustomkey/test");
                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var widget = await stateClient.GetStateAsync<Widget>("test");
                widget.Count.Should().Be(18);
            }
        }
    }
}