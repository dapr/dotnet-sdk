using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapr.AspNetCore;
using Dapr.Client;
using Grpc.Core;
using GrpcServiceSample.Generated;
using Microsoft.Extensions.Logging;

namespace GrpcServiceSample
{
    public class BankingService2 : GrpcBaseService
    {
        /// <summary>
        /// State store name.
        /// </summary>
        public const string StoreName = "statestore";

        private readonly ILogger<BankingService2> _logger;
        private readonly DaprClient _daprClient;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="daprClient"></param>
        /// <param name="logger"></param>
        public BankingService2(DaprClient daprClient, ILogger<BankingService2> logger)
        {
            _daprClient = daprClient;
            _logger = logger;
        }

        /// <summary>
        /// GetAccount
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [GrpcInvoke]
        public async Task<Account> GetAccount(GetAccountRequest input, ServerCallContext context)
        {
            var state = await _daprClient.GetStateEntryAsync<Models.Account>(StoreName, input.Id);
            return new Account() { Id = state.Value.Id, Balance = (int)state.Value.Balance, };
        }

        /// <summary>
        /// Deposit
        /// </summary>
        /// <param name="transaction"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [GrpcInvoke]
        public async Task<Account> Deposit(Transaction transaction, ServerCallContext context)
        {
            _logger.LogDebug("Enter deposit");
            var state = await _daprClient.GetStateEntryAsync<Models.Account>(StoreName, transaction.Id);
            state.Value ??= new Models.Account() { Id = transaction.Id, };
            state.Value.Balance += transaction.Amount;
            await state.SaveAsync();
            return new Account() { Id = state.Value.Id, Balance = (int)state.Value.Balance, };
        }

        /// <summary>
        /// Withdraw
        /// </summary>
        /// <param name="transaction"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        [GrpcInvoke]
        public async Task<Account> Withdraw(Transaction transaction, ServerCallContext context)
        {
            _logger.LogDebug("Enter withdraw");
            var state = await _daprClient.GetStateEntryAsync<Models.Account>(StoreName, transaction.Id);

            if (state.Value == null)
            {
                throw new Exception($"NotFound: {transaction.Id}");
            }

            state.Value.Balance -= transaction.Amount;
            await state.SaveAsync();
            return new Account() { Id = state.Value.Id, Balance = (int)state.Value.Balance, };
        }
    }
}
