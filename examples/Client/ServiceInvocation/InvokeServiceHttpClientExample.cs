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
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;

namespace Samples.Client;

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