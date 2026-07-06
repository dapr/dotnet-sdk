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
using System.Text.Json;
using System.Threading.Tasks;
using Dapr.AppCallback.Autogen.Grpc.v1;
using Dapr.Client;
using Dapr.Client.Autogen.Grpc.v1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using GrpcServiceSample.Generated;
using Microsoft.Extensions.Logging;

namespace GrpcServiceSample.Services;

/// <summary>
/// BankAccount gRPC service
/// </summary>
public class BankingService : AppCallback.AppCallbackBase
{
    /// <summary>
    /// State store name.
    /// </summary>
    public const string StoreName = "statestore";

    private readonly ILogger<BankingService> _logger;
    private readonly DaprClient _daprClient;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="daprClient"></param>
    /// <param name="logger"></param>
    public BankingService(DaprClient daprClient, ILogger<BankingService> logger)
    {
        _daprClient = daprClient;
        _logger = logger;
    }

    readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// implement OnIvoke to support getaccount, deposit and withdraw
    /// </summary>
    /// <param name="request"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public override async Task<InvokeResponse> OnInvoke(InvokeRequest request, ServerCallContext context)
    {
        var response = new InvokeResponse();
        switch (request.Method)
        {
            case "getaccount":                
                var input = request.Data.Unpack<GrpcServiceSample.Generated.GetAccountRequest>();
                var output = await GetAccount(input, context);
                response.Data = Any.Pack(output);
                break;
            case "deposit":
            case "withdraw":
                var transaction = request.Data.Unpack<GrpcServiceSample.Generated.Transaction>();
                var account = request.Method == "deposit" ?
                    await Deposit(transaction, context) :
                    await Withdraw(transaction, context);
                response.Data = Any.Pack(account);
                break;
            default:
                break;
        }
        return response;
    }

    /// <summary>
    /// implement ListTopicSubscriptions to register deposit and withdraw subscriber
    /// </summary>
    /// <param name="request"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public override Task<ListTopicSubscriptionsResponse> ListTopicSubscriptions(Empty request, ServerCallContext context)
    {
        var result = new ListTopicSubscriptionsResponse();
        result.Subscriptions.Add(new TopicSubscription
        {
            PubsubName = "pubsub",
            Topic = "deposit"
        });
        result.Subscriptions.Add(new TopicSubscription
        {
            PubsubName = "pubsub",
            Topic = "withdraw"
        });
        return Task.FromResult(result);
    }

    /// <summary>
    /// implement OnTopicEvent to handle deposit and withdraw event
    /// </summary>
    /// <param name="request"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public override async Task<TopicEventResponse> OnTopicEvent(TopicEventRequest request, ServerCallContext context)
    {
        if (request.PubsubName == "pubsub")
        {
            var input = JsonSerializer.Deserialize<Models.Transaction>(request.Data.ToStringUtf8(), this.jsonOptions);
            var transaction = new GrpcServiceSample.Generated.Transaction() { Id = input.Id, Amount = (int)input.Amount, };
            if (request.Topic == "deposit")
            {
                await Deposit(transaction, context);
            }
            else
            {
                await Withdraw(transaction, context);
            }
        }

        return new TopicEventResponse();
    }

    /// <summary>
    /// GetAccount
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task<GrpcServiceSample.Generated.Account> GetAccount(GetAccountRequest input, ServerCallContext context)
    {
        var state = await _daprClient.GetStateEntryAsync<Models.Account>(StoreName, input.Id);
        return new GrpcServiceSample.Generated.Account() { Id = state.Value.Id, Balance = (int)state.Value.Balance, }; 
    }

    /// <summary>
    /// Deposit
    /// </summary>
    /// <param name="transaction"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task<GrpcServiceSample.Generated.Account> Deposit(GrpcServiceSample.Generated.Transaction transaction, ServerCallContext context)
    {
        _logger.LogDebug("Enter deposit");
        var state = await _daprClient.GetStateEntryAsync<Models.Account>(StoreName, transaction.Id);
        state.Value ??= new Models.Account() { Id = transaction.Id, };
        state.Value.Balance += transaction.Amount;
        await state.SaveAsync();
        return new GrpcServiceSample.Generated.Account() { Id = state.Value.Id, Balance = (int)state.Value.Balance, }; 
    }

    /// <summary>
    /// Withdraw
    /// </summary>
    /// <param name="transaction"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task<GrpcServiceSample.Generated.Account> Withdraw(GrpcServiceSample.Generated.Transaction transaction, ServerCallContext context)
    {
        _logger.LogDebug("Enter withdraw");
        var state = await _daprClient.GetStateEntryAsync<Models.Account>(StoreName, transaction.Id);

        if (state.Value == null)
        {
            throw new Exception($"NotFound: {transaction.Id}");
        }

        state.Value.Balance -= transaction.Amount;
        await state.SaveAsync();
        return new GrpcServiceSample.Generated.Account() { Id = state.Value.Id, Balance = (int)state.Value.Balance, };
    }
}