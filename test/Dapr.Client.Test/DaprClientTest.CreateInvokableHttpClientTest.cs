// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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
using Xunit;

namespace Dapr.Client
{
    public partial class DaprClientTest
    {
        [Fact]
        public void CreateInvokableHttpClient_WithAppId_FromDaprClient()
        {
            var daprClient = new MockClient().DaprClient;
            var client = daprClient.CreateInvokableHttpClient(appId: "bank", daprEndpoint: "http://localhost:3500");
            Assert.Equal("http://bank/", client.BaseAddress.AbsoluteUri);
        }

        [Fact]
        public void CreateInvokableHttpClient_InvalidAppId_FromDaprClient()
        {
            var daprClient = new MockClient().DaprClient;
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                // The appId needs to be something that can be used as hostname in a URI.
                _ = daprClient.CreateInvokableHttpClient(appId: "");
            });

            Assert.Contains("The appId must be a valid hostname.", ex.Message);
            Assert.IsType<UriFormatException>(ex.InnerException);
        }

        [Fact]
        public void CreateInvokableHttpClient_WithoutAppId_FromDaprClient()
        {
            var daprClient = new MockClient().DaprClient;
            var client = daprClient.CreateInvokableHttpClient(daprEndpoint: "http://localhost:3500");
            Assert.Null(client.BaseAddress);
        }

        [Fact]
        public void CreateInvokableHttpClient_InvalidDaprEndpoint_InvalidFormat_FromDaprClient()
        {
            var daprClient = new MockClient().DaprClient;
            Assert.Throws<UriFormatException>(() =>
            {
                _ = daprClient.CreateInvokableHttpClient(daprEndpoint: "");
            });

            // Exception message comes from the runtime, not validating it here.
        }

        [Fact]
        public void CreateInvokableHttpClient_InvalidDaprEndpoint_InvalidScheme_FromDaprClient()
        {
            var daprClient = new MockClient().DaprClient;
            var ex = Assert.Throws<ArgumentException>(() =>
            {
                _ = daprClient.CreateInvokableHttpClient(daprEndpoint: "ftp://localhost:3500");
            });

            Assert.Contains("The URI scheme of the Dapr endpoint must be http or https.", ex.Message);
        }
    }
}
