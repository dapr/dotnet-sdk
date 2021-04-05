using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Dapr.AspNetCore.Test.Generated;
using Dapr.Client;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Dapr.AspNetCore.Test
{
    public class HappyGrpcService : GrpcBaseService
    {
        public HappyGrpcService(DaprClient daprClient, ILogger logger) : base(daprClient, logger)
        {
        }

        [GrpcInvoke(typeof(AccountRequest))]
        public Task<Account> GetAccount(AccountRequest model, ServerCallContext context)
        {
            return Task.FromResult(new Account { Id = "test", Balance = 123 });
        }

        [GrpcInvoke(typeof(AccountRequest), "rich")]
        public Task<Transaction> LetUsRich(AccountRequest model, ServerCallContext context)
        {
            return Task.FromResult(new Transaction { Id = "test", Amount = 100000 });
        }
    }
}
