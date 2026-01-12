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
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;

namespace Samples.Client;

public class InvokeServiceHttpExample : Example
{
    public override string DisplayName => "Invoking an HTTP service with DaprClient";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        using var client = new DaprClientBuilder().Build();

        // Invokes a POST method named "deposit" that takes input of type "Transaction" as define in the RoutingSample.
        Console.WriteLine("Invoking deposit");
        var data = new { id = "17", amount = 99m };
        var account = await client.InvokeMethodAsync<object, Account>("routing", "deposit", data, cancellationToken);
        Console.WriteLine("Returned: id:{0} | Balance:{1}", account.Id, account.Balance);

        // Invokes a POST method named "Withdraw" that takes input of type "Transaction" as define in the RoutingSample.
        Console.WriteLine("Invoking withdraw");
        data = new { id = "17", amount = 10m, };
        await client.InvokeMethodAsync<object>("routing", "Withdraw", data, cancellationToken);
        Console.WriteLine("Completed");

        // Invokes a GET method named "hello" that takes input of type "MyData" and returns a string.
        Console.WriteLine("Invoking balance");
        account = await client.InvokeMethodAsync<Account>(HttpMethod.Get, "routing", "17", cancellationToken);
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