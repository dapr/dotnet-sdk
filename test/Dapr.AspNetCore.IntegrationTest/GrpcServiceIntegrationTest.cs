using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;
using FluentAssertions;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using Xunit;

namespace Dapr.AspNetCore.IntegrationTest
{
    public class GrpcServiceIntegrationTest
    {
        private readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        [Fact]
        public async Task ServiceInvocation_CanOnInvoke()
        {
            using (var factory = new AppWebApplicationFactory())
            {
                var client = CreateAppCallbackClient(factory);

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

                request = new App.Generated.InvokeRequest()
                {
                    Method = "grpcservicedeposit",
                    Data = Any.Pack(new App.Generated.AccountRequest { Id = "any" })
                };
                response = await client.OnInvokeAsync(request);
                response.Data.Should().BeNull();
            }
        }

        private static App.Generated.AppCallback.AppCallbackClient CreateAppCallbackClient(AppWebApplicationFactory factory)
        {
            var channel = GrpcChannel.ForAddress("https://localhost", new GrpcChannelOptions
            {
                HttpClient = factory.CreateDefaultClient(new ResponseVersionHandler())
            });
            return new App.Generated.AppCallback.AppCallbackClient(channel);
        }

        [Fact]
        public async Task PubSub_CanListTopic()
        {
            using (var factory = new AppWebApplicationFactory())
            {
                var client = CreateAppCallbackClient(factory);

                var response = await client.ListTopicSubscriptionsAsync(new Empty());
                response.Subscriptions.Count.Should().Be(2);

                response.Subscriptions.All(p => p.PubsubName == "pubsub").Should().BeTrue();
                response.Subscriptions.Any(p => p.Topic == "deposit").Should().BeTrue();
                response.Subscriptions.Any(p => p.Topic == "withdraw").Should().BeTrue();
            }
        }

        [Fact]
        public async Task PubSub_CanOnTopic()
        {
            using (var factory = new AppWebApplicationFactory())
            {
                var client = CreateAppCallbackClient(factory);

                var input = new App.Generated.AccountRequest { Id = "any" };

                var request = new App.Generated.TopicEventRequest
                {
                    PubsubName = "pubsub",
                    Topic = "deposit",
                    Data = TypeConverters.ToJsonByteString(input, jsonOptions)
                };
                var response = await client.OnTopicEventAsync(request);
                response.Status.Should().Be(App.Generated.TopicEventResponse.Types.TopicEventResponseStatus.Success);

                request = new App.Generated.TopicEventRequest
                {
                    PubsubName = "pubsub",
                    Topic = "withdraw",
                    Data = TypeConverters.ToJsonByteString(input, jsonOptions)
                };
                response = await client.OnTopicEventAsync(request);
                response.Status.Should().Be(App.Generated.TopicEventResponse.Types.TopicEventResponseStatus.Success);
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
