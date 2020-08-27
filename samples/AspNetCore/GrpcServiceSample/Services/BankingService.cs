using System;
using System.Text.Json;
using System.Threading.Tasks;
using Dapr.AppCallback.Autogen.Grpc.v1;
using Dapr.Client;
using Dapr.Client.Autogen.Grpc.v1;
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

        public async Task<Account> Deposit(Transaction transaction, ServerCallContext context)
        {
            Console.WriteLine("Enter deposit");
            var state = await _daprClient.GetStateEntryAsync<Account>(StoreName, transaction.Id);
            state.Value ??= new Account() { Id = transaction.Id, };
            state.Value.Balance += transaction.Amount;
            await state.SaveAsync();
            return state.Value;
        }

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
