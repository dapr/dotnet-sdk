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
namespace Dapr.E2E.Test;

using System;
using System.Net.Http;
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

    [Fact]
    public async Task TestServiceInvocationRequiresApiToken()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        using var client = DaprClient.CreateInvokeHttpClient(appId: this.AppId, daprEndpoint: this.HttpEndpoint);

        var transaction = new Transaction()
        {
            Id = "1",
            Amount = 50
        };
        var response = await client.PostAsJsonAsync<Transaction>("/accountDetails-requires-api-token", transaction, cts.Token);
        var (account, _) = await HttpAssert.AssertJsonResponseAsync<Account>(response);

        Assert.Equal("1", account.Id);
        Assert.Equal(150, account.Balance);
    }

    [Fact]
    public async Task TestHttpServiceInvocationWithTimeout()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var client = new DaprClientBuilder()
            .UseHttpEndpoint(this.HttpEndpoint)
            .UseTimeout(TimeSpan.FromSeconds(1))
            .Build();

        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
        {
            await client.InvokeMethodAsync<HttpResponseMessage>(
                appId: this.AppId,
                methodName: "DelayedResponse",
                httpMethod: new HttpMethod("GET"),
                cancellationToken: cts.Token);
        });
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