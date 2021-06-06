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
        [GrpcInvoke]
        public Task<Account> GetAccount(AccountRequest model, AnotherAccountRequest model2)
        {
            return Task.FromResult(new Account { Id = "test", Balance = 123 });
        }
    }

    public class UnhappyGrpcService1 : GrpcBaseService
    {
        [GrpcInvoke]
        public Task<Account> GetAccount(AnotherAccountRequest model)
        {
            return Task.FromResult(new Account { Id = "test", Balance = 123 });
        }
    }

    public class UnhappyGrpcService2 : GrpcBaseService
    {
        [GrpcInvoke]
        public Account GetAccount(AccountRequest model)
        {
            return new Account { Id = "test", Balance = 123 };
        }
    }

    public class UnhappyGrpcService3 : GrpcBaseService
    {
        [GrpcInvoke]
        public List<Account> GetAccount(AccountRequest model)
        {
            return new List<Account> { new Account { Id = "test", Balance = 123 } };
        }
    }

    public class UnhappyGrpcService4 : GrpcBaseService
    {
        [GrpcInvoke]
        public Task<AnotherAccount> GetAccount(AccountRequest model)
        {
            return Task.FromResult(new AnotherAccount { Id = "test", Balance = 123 });
        }
    }
}
