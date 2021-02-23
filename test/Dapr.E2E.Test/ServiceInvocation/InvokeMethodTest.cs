// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------
namespace Dapr.E2E.Test
{
    using System;
    using System.Net.Http.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.Client;
    using Xunit;
    using Xunit.Abstractions;

    public class ServiceInvocationTests : IDisposable
    {
        private DaprTestApp testApp;

        public ServiceInvocationTests(ITestOutputHelper testOutput)
        {
            this.testApp = new DaprTestApp(testOutput, "testapp", true);
        }

        public void Dispose()
        {
            this.testApp.Stop();
        }

        [Fact]
        public async Task TestServiceInvocation()
        {
            var (daprHttpEndpoint, _) = this.testApp.Start();
            var client = DaprClient.CreateInvokeHttpClient(appId: "testApp", daprEndpoint: daprHttpEndpoint);
            var cts = new CancellationTokenSource();
            var transaction = new Transaction()
            {
                Id = "1",
                Amount = 50
            };
            var response = await client.PostAsJsonAsync<Transaction>("/accountDetails", transaction, cts.Token);
            var account = await response.Content.ReadFromJsonAsync<Account>();
            Assert.Equal("1", account.Id);
            Assert.Equal(150, account.Balance);
        }
    }

    internal class Transaction
    {
        public string Id { get; set; }
        public decimal Amount { get; set; }
    }

    internal class Account
    {
        public string Id { get; set; }
        public decimal Balance { get; set; }
    }
}