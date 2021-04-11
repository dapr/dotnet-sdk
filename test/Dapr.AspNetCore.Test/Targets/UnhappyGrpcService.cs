using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Dapr.AspNetCore.Test.Generated;
using Grpc.Core;

namespace Dapr.AspNetCore.Test.Targets
{
    public class UnhappyGrpcService0 : GrpcBaseService
    {
        [GrpcInvoke(typeof(AccountRequest))]
        public Task<Account> GetAccount(AccountRequest model)
        {
            return Task.FromResult(new Account { Id = "test", Balance = 123 });
        }
    }

    public class UnhappyGrpcService1 : GrpcBaseService
    {
        [GrpcInvoke(typeof(AccountRequest))]
        public Task<Account> GetAccount(AnotherAccountRequest model, ServerCallContext context)
        {
            return Task.FromResult(new Account { Id = "test", Balance = 123 });
        }
    }

    public class UnhappyGrpcService2 : GrpcBaseService
    {
        [GrpcInvoke(typeof(Transaction))]
        public Task<Account> GetAccount(AccountRequest model, ServerCallContext context)
        {
            return Task.FromResult(new Account { Id = "test", Balance = 123 });
        }
    }

    public class UnhappyGrpcService3 : GrpcBaseService
    {
        [GrpcInvoke(typeof(AccountRequest))]
        public Task<Account> GetAccount(AccountRequest model, AnotherAccountRequest model2)
        {
            return Task.FromResult(new Account { Id = "test", Balance = 123 });
        }
    }

    public class UnhappyGrpcService4 : GrpcBaseService
    {
        [GrpcInvoke(typeof(AccountRequest))]
        public Account GetAccount(AccountRequest model, ServerCallContext context)
        {
            return new Account { Id = "test", Balance = 123 };
        }
    }

    public class UnhappyGrpcService5 : GrpcBaseService
    {
        [GrpcInvoke(typeof(AccountRequest))]
        public Task<AnotherAccount> GetAccount(AccountRequest model, ServerCallContext context)
        {
            return Task.FromResult(new AnotherAccount { Id = "test", Balance = 123 });
        }
    }
}
