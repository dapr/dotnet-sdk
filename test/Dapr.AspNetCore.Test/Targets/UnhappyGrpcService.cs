using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Dapr.AspNetCore.Test.Generated;
using Grpc.Core;

namespace Dapr.AspNetCore.Test.Targets
{
    public class UnhappyGrpcService1 : GrpcBaseService
    {
        [GrpcInvoke(typeof(AccountRequest))]
        public Task<Account> GetAccount(AccountRequest model)
        {
            return Task.FromResult(new Account { Id = "test", Balance = 123 });
        }
    }

    public class UnhappyGrpcService2 : GrpcBaseService
    {
        [GrpcInvoke(typeof(AccountRequest))]
        public Task<Account> GetAccount(AnotherAccountRequest model, ServerCallContext context)
        {
            return Task.FromResult(new Account { Id = "test", Balance = 123 });
        }

        public class AnotherAccountRequest
        {
            public string Id { get; set; }
        }
    }
}
