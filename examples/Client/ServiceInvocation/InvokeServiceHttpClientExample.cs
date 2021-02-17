// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;

namespace Samples.Client
{
    public class InvokeServiceHttpClientExample : Example
    {
        public override string DisplayName => "Invoking an HTTP service with HttpClient";

        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            var client = DaprClient.CreateInvokeHttpClient(appId: "routing");

            var deposit = new Transaction  { Id = "17", Amount = 99m };
            var response = await client.PostAsJsonAsync("/deposit", deposit, cancellationToken);
            var account = await response.Content.ReadFromJsonAsync<Account>(cancellationToken: cancellationToken);
            Console.WriteLine("Returned: id:{0} | Balance:{1}", account?.Id, account?.Balance);

            var withdraw = new Transaction { Id = "17", Amount = 10m, };
            response = await client.PostAsJsonAsync("/withdraw", withdraw, cancellationToken);
            response.EnsureSuccessStatusCode();

            account = await client.GetFromJsonAsync<Account>("/17", cancellationToken);
            Console.WriteLine($"Received balance {account?.Balance}");
        }

        internal class Transaction
        {
            public string? Id { get; set; }

            public decimal? Amount { get; set; }
        }

        internal class Account
        {
            public string? Id { get; set; }

            public decimal? Balance { get; set; }
        }
    }
}
