// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------
namespace Dapr.E2E.Test
{
    using System;
    using System.Threading.Tasks;
    using Dapr.Client;
    using Xunit;

    public class ServiceInvocationTests : IDisposable
    {
        private DaprApp testApp;
        public ServiceInvocationTests()
        {
            this.testApp = new DaprApp("testapp", true);
        }

        public void Dispose()
        {
            this.testApp.Stop();
        }

        [Fact]
        public async Task TestServiceInvocation()
        {
            var (daprHttpEndpoint, _) = this.testApp.Start();
            var client = DaprClient.CreateInvokeHttpClient(appId: "testapp", daprEndpoint: daprHttpEndpoint);
            var response = await client.GetStringAsync("/hello/John");
            Assert.Equal("Hello John!", response);
        }
    }
}