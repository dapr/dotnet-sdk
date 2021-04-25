using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Dapr.AspNetCore.Test.Generated;
using Dapr.Client;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Dapr.AspNetCore.Test.Targets
{
    public class HappyGrpcService : GrpcBaseService
    {
        [GrpcInvoke]
        public Task<Account> GetAccount(AccountRequest model)
        {
            return Task.FromResult(new Account { Id = "test", Balance = 123 });
        }

        [GrpcInvoke("deposit")]
        [Topic("pubsub", "deposit")]
        public Task<Transaction> DepositAccount(AccountRequest model)
        {
            return Task.FromResult(new Transaction { Id = "test", Amount = 100000 });
        }

        [GrpcInvoke("withdraw")]
        [Topic("pubsub", "withdraw")]
        public Task<Transaction> WithdrawAccount(AccountRequest model)
        {
            return Task.FromResult(new Transaction { Id = "test", Amount = 100 });
        }      

        [GrpcInvoke]
        public Task Close(AccountRequest model)
        {
            return Task.CompletedTask;
        }

        public void Another()
        {
            throw new NotSupportedException();
        }
    }
}
