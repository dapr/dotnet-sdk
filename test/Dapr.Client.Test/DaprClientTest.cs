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
        public void CreateInvokeHttpClient_WithAppId()
        {
            var client = DaprClient.CreateInvokeHttpClient(appId: "bank", daprEndpoint: "http://localhost:3500");
            Assert.Equal("http://bank/", client.BaseAddress.AbsoluteUri);
        }

        [Fact]
        public void CreateInvokeHttpClient_WithoutAppId()
        {
            var client = DaprClient.CreateInvokeHttpClient(daprEndpoint: "http://localhost:3500");
            Assert.Null(client.BaseAddress);
        }
    }
}
