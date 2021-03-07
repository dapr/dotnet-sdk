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

    public partial class E2ETests
    {
        [Fact]
        public async Task TestServiceInvocation()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            using var client = DaprClient.CreateInvokeHttpClient(appId: this.AppId, daprEndpoint: this.HttpEndpoint);
            var transaction = new Transaction()
            {
                Id = "1",
                Amount = 50
            };

            var response = await client.PostAsJsonAsync<Transaction>("/accountDetails", transaction, cts.Token);
            var (account, _) = await HttpAssert.AssertJsonResponseAsync<Account>(response);

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
