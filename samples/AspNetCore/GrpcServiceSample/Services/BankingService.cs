// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Dapr.AppCallback.Autogen.Grpc.v1;
using Dapr.Client;
using Dapr.Client.Autogen.Grpc.v1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using GrpcServiceSample.Models;
using Microsoft.Extensions.Logging;

namespace GrpcServiceSample
{
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
            var respone = new InvokeResponse();
            switch (request.Method)
            {
                case "getaccount":
                    var input = TypeConverters.FromAny<GetAccountInput>(request.Data, this.jsonOptions);
                    var output = await GetAccount(input, context);
                    respone.Data = TypeConverters.ToAny<Account>(output, this.jsonOptions);
                    break;
                case "deposit":
                case "withdraw":
                    var transaction = TypeConverters.FromAny<Transaction>(request.Data, this.jsonOptions);
                    var account = request.Method == "deposit" ?
                        await Deposit(transaction, context) :
                        await Withdraw(transaction, context);
                    respone.Data = TypeConverters.ToAny<Account>(account, this.jsonOptions);
                    break;
                default:
                    break;
            }
            return respone;
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
                var transaction = JsonSerializer.Deserialize<Transaction>(request.Data.ToStringUtf8(), this.jsonOptions);
                if (request.Topic == "deposit")
                    await Deposit(transaction, context);
                else
                    await Withdraw(transaction, context);
            }
           
            return await Task.FromResult(default(TopicEventResponse));
        }

        /// <summary>
        /// GetAccount
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<Account> GetAccount(GetAccountInput input, ServerCallContext context)
        {
            var state = await _daprClient.GetStateEntryAsync<Account>(StoreName, input.Id);

            if (state.Value == null)
            {
                return null;
            }

            return state.Value;
        }

        /// <summary>
        /// Deposit
        /// </summary>
        /// <param name="transaction"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<Account> Deposit(Transaction transaction, ServerCallContext context)
        {
            Console.WriteLine("Enter deposit");
            var state = await _daprClient.GetStateEntryAsync<Account>(StoreName, transaction.Id);
            state.Value ??= new Account() { Id = transaction.Id, };
            state.Value.Balance += transaction.Amount;
            await state.SaveAsync();
            return state.Value;
        }

        /// <summary>
        /// Withdraw
        /// </summary>
        /// <param name="transaction"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<Account> Withdraw(Transaction transaction, ServerCallContext context)
        {
            Console.WriteLine("Enter withdraw");
            var state = await _daprClient.GetStateEntryAsync<Account>(StoreName, transaction.Id);

            if (state.Value == null)
            {
                return null;
            }

            state.Value.Balance -= transaction.Amount;
            await state.SaveAsync();
            return state.Value;
        }
    }
}
