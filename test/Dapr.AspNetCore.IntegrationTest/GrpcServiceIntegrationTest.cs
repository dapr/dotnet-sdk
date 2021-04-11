using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using Xunit;

namespace Dapr.AspNetCore.IntegrationTest
{
    public class GrpcServiceIntegrationTest
    {
        [Fact]
        public async Task ServiceInvocation_CanInvokeMethod()
        {
            using (var factory = new AppWebApplicationFactory())
            {
                var channel = GrpcChannel.ForAddress("https://localhost", new GrpcChannelOptions
                {
                    HttpClient = factory.CreateDefaultClient(new ResponseVersionHandler())
                });
                var client = new App.Generated.AppCallback.AppCallbackClient(channel);

                var request = new App.Generated.InvokeRequest()
                {
                    Method = "grpcservicegetaccount",
                    Data = Any.Pack(new App.Generated.AccountRequest { Id = "any" })
                };
                var response = await client.OnInvokeAsync(request);
                var content1 = response.Data.Unpack<App.Generated.Account>();
                content1.Id.Should().Be("test");
                content1.Balance.Should().Be(123);

                request = new App.Generated.InvokeRequest()
                {
                    Method = "grpcservicewithdraw",
                    Data = Any.Pack(new App.Generated.AccountRequest { Id = "any" })
                };
                response = await client.OnInvokeAsync(request);
                var content2 = response.Data.Unpack<App.Generated.Transaction>();
                content2.Id.Should().Be("test");
                content2.Amount.Should().Be(100000);
            }
        }

        private class ResponseVersionHandler : DelegatingHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                var response = await base.SendAsync(request, cancellationToken);
                response.Version = request.Version;
                return response;
            }
        }
    }
}
