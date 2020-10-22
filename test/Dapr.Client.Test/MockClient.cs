using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client.Http;
using Grpc.Core;
using Moq;

namespace Dapr.Client
{
    public class MockClient
    {
        public MockClient()
        {
            Mock = new Mock<Autogen.Grpc.v1.Dapr.DaprClient>(MockBehavior.Strict);
            DaprClient = new DaprClientGrpc(Mock.Object, new JsonSerializerOptions());
        }

        public Mock<Autogen.Grpc.v1.Dapr.DaprClient> Mock { get; }

        public DaprClient DaprClient { get; }

        public CallBuilder<TResponse> Call<TResponse>()
        {
            return new CallBuilder<TResponse>();
        }

        public class CallBuilder<TResponse>
        {
            private TResponse response;
            private Metadata headers;
            private Status status;
            private Metadata trailers;

            public CallBuilder()
            {
                headers = new Metadata();
                trailers = new Metadata();
            }

            public AsyncUnaryCall<TResponse> Build()
            {
                return new AsyncUnaryCall<TResponse>(
                    Task.FromResult(response),
                    Task.FromResult(headers),
                    () => status,
                    () => trailers,
                    () => {});
            }

            public CallBuilder<TResponse> SetResponse(TResponse response)
            {
                this.response = response;
                return this;
            }

            public CallBuilder<TResponse> SetStatus(Status status)
            {
                this.status = status;
                return this;
            }

            public CallBuilder<TResponse> AddHeader(string key, string value)
            {
                this.headers.Add(key, value);
                return this;
            }

            public CallBuilder<TResponse> AddTrailer(string key, string value)
            {
                this.trailers.Add(key, value);
                return this;
            }
        }
    }
}