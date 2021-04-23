using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapr.AspNetCore.IntegrationTest.App.Generated;
using Grpc.Core;

namespace Dapr.AspNetCore.IntegrationTest.App
{
    public class DaprGrpcService : GrpcBaseService
    {
        [GrpcInvoke("grpcservicegetaccount")]
        public Task<Account> GetAccount(AccountRequest model)
        {
            return Task.FromResult(new Account { Id = "test", Balance = 123 });
        }

        [GrpcInvoke("grpcservicewithdraw")]
        public Task<Transaction> WithdrawAccount(AccountRequest model)
        {
            return Task.FromResult(new Transaction { Id = "test", Amount = 100000 });
        }

        [GrpcInvoke("grpcservicedeposit")]
        public Task DepositAccount(AccountRequest model)
        {
            return Task.CompletedTask;
        }
    }
}
