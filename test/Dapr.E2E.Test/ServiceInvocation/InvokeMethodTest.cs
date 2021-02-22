// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------
namespace Dapr.E2E.Test
{
    using System;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.Client;
    using Xunit;

    public class ServiceInvocationTests
    {
        [Fact]
        public async Task TestServiceInvocation()
        {
            var testApp = new DaprApp("testapp", 5000);
            try
            {
                var (daprHttpEndpoint, _) = testApp.Start();
                var client = DaprClient.CreateInvokeHttpClient(appId: "testapp", daprEndpoint: daprHttpEndpoint);
                var response = await client.GetStringAsync("/hello/John");
                Assert.Equal("Hello John!", response);
            }
            finally
            {
                testApp.Stop();
            }
        }
    }
}