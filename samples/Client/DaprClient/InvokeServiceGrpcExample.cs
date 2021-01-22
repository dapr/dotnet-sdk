// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;
using GrpcServiceSample.Generated;

namespace Samples.Client
{
    public class InvokeServiceGrpcExample : Example
    {
        public override string DisplayName => "Invoking a gRPC service with gRPC semantics and Protobuf with DaprClient";

        // Note: the data types used in this sample are generated from data.proto in GrpcServiceSample
        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            var client = new DaprClientBuilder().Build();

            Console.WriteLine("Invoking grpc balance");
            var request = new GetAccountRequest() { Id = "17", };
            var account = await client.InvokeMethodGrpcAsync<GetAccountRequest, Account>("grpcsample", "getaccount", request, cancellationToken);
            Console.WriteLine($"Received grpc balance {account.Balance}");

            Console.WriteLine("Invoking grpc deposit");
            var data = new GrpcServiceSample.Generated.Transaction() { Id = "17", Amount = 99 };
            account = await client.InvokeMethodGrpcAsync<Transaction, Account>("grpcsample", "deposit", data, cancellationToken);
            Console.WriteLine("Returned: id:{0} | Balance:{1}", account.Id, account.Balance);
            Console.WriteLine("Completed grpc deposit");

            Console.WriteLine("Invoking grpc withdraw");
            var withdraw = new Transaction() { Id = "17", Amount = 10, };
            await client.InvokeMethodGrpcAsync("grpcsample", "withdraw", data, cancellationToken);
            Console.WriteLine("Completed grpc withdraw");
        }
    }
}
