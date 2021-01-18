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
    public class InvokeServiceGrpcExample : Example
    {
        public override string DisplayName => "Invoking a gRPC service with DaprClient";

        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            var client = new DaprClientBuilder().Build();

            Console.WriteLine("Invoking grpc balance");
            var account = await client.InvokeMethodAsync<object, Account>("grpcsample", "getaccount", new { Id = "17" }, cancellationToken: cancellationToken);
            Console.WriteLine($"Received grpc balance {account.Balance}");

            Console.WriteLine("Invoking grpc deposit");
            var data = new Transaction() { Id = "17", Amount = 99m };
            account = await client.InvokeMethodAsync<Transaction, Account>("grpcsample", "deposit", data, cancellationToken: cancellationToken);
            Console.WriteLine("Returned: id:{0} | Balance:{1}", account.Id, account.Balance);
            Console.WriteLine("Completed grpc deposit");

            Console.WriteLine("Invoking grpc withdraw");
            var withdraw = new Transaction() { Id = "17", Amount = 10m, };
            await client.InvokeMethodAsync("grpcsample", "withdraw", data, cancellationToken: cancellationToken);
            Console.WriteLine("Completed grpc withdraw");
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
