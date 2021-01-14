// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using Xunit;

namespace Dapr.Client
{
    public class DaprClientTest
    {
        [Fact]
        public void CreateInvokeClient_WithAppId()
        {
            var client = DaprClient.CreateInvokeClient(appId: "bank", daprEndpoint: "http://localhost:3500");
            Assert.Equal("http://bank/", client.BaseAddress.AbsoluteUri);
        }

        [Fact]
        public void CreateInvokeClient_WithoutAppId()
        {
            var client = DaprClient.CreateInvokeClient(daprEndpoint: "http://localhost:3500");
            Assert.Null(client.BaseAddress);
        }
    }
}
