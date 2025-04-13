// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;
using GrpcServiceSample.Generated;

namespace Samples.Client;

public class InvokeServiceGrpcExample : Example
{
    public override string DisplayName => "Invoking a gRPC service with gRPC semantics and Protobuf with DaprClient";

    // Note: the data types used in this sample are generated from data.proto in GrpcServiceSample
    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        using var client = new DaprClientBuilder().Build();

        Console.WriteLine("Invoking grpc deposit");
        var deposit = new GrpcServiceSample.Generated.Transaction() { Id = "17", Amount = 99 };
        var account = await client.InvokeMethodGrpcAsync<GrpcServiceSample.Generated.Transaction, Account>("grpcsample", "deposit", deposit, cancellationToken);
        Console.WriteLine("Returned: id:{0} | Balance:{1}", account.Id, account.Balance);
        Console.WriteLine("Completed grpc deposit");

        Console.WriteLine("Invoking grpc withdraw");
        var withdraw = new GrpcServiceSample.Generated.Transaction() { Id = "17", Amount = 10, };
        await client.InvokeMethodGrpcAsync("grpcsample", "withdraw", withdraw, cancellationToken);
        Console.WriteLine("Completed grpc withdraw");

        Console.WriteLine("Invoking grpc balance");
        var request = new GetAccountRequest() { Id = "17", };
        account = await client.InvokeMethodGrpcAsync<GetAccountRequest, Account>("grpcsample", "getaccount", request, cancellationToken);
        Console.WriteLine($"Received grpc balance {account.Balance}");
    }
}