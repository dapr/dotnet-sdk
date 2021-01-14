// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;

namespace Samples.Client
{
    public class InvokeServiceHttpExample : Example
    {
        public override string DisplayName => "Invoking an HTTP service with DaprClient";

        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            var client = new DaprClientBuilder().Build();

            // Invokes a POST method named "deposit" that takes input of type "Transaction" as define in the RoutingSample.
            Console.WriteLine("Invoking deposit");
            var data = new { id = "17", amount = 99m };
            var account = await client.InvokeMethodAsync<object, Account>("routing", "deposit", data, HttpInvocationOptions.UsingPost(), cancellationToken);
            Console.WriteLine("Returned: id:{0} | Balance:{1}", account.Id, account.Balance);

            // Invokes a POST method named "Withdraw" that takes input of type "Transaction" as define in the RoutingSample.
            Console.WriteLine("Invoking withdraw");
            data = new { id = "17", amount = 10m, };
            await client.InvokeMethodAsync<object>("routing", "Withdraw", data, HttpInvocationOptions.UsingPost(), cancellationToken);
            Console.WriteLine("Completed");

            // Invokes a GET method named "hello" that takes input of type "MyData" and returns a string.
            Console.WriteLine("Invoking balance");
            account = await client.InvokeMethodAsync<Account>("routing", "17", HttpInvocationOptions.UsingGet(), cancellationToken);
            Console.WriteLine($"Received balance {account.Balance}");
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
