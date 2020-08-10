using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly ILogger<BankingService> _logger;
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger"></param>
        public BankingService(ILogger<BankingService> logger)
        {
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
                    respone.Data = TypeConverters.ToAny<GetAccountOutput>(output, this.jsonOptions);
                    break;
                default:
                    break;
            }
            return respone;
        }
       
        /// <summary>
        /// GetAccount
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public Task<GetAccountOutput> GetAccount(GetAccountInput request, ServerCallContext context)
        {
            return Task.FromResult(new GetAccountOutput
            {
                Id = Guid.NewGuid().ToString(),
                Balance = 100
            });
        }
    }
}
