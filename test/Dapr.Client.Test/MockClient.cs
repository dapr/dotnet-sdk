// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    using System.Text.Json;
    using System.Threading.Tasks;
    using Grpc.Core;
    using Moq;

    public class MockClient
    {
        public MockClient()
        {
            Mock = new Mock<Autogen.Grpc.v1.Dapr.DaprClient>(MockBehavior.Strict);
            DaprClient = new DaprClientGrpc(Mock.Object, new JsonSerializerOptions());
        }

        public Mock<Autogen.Grpc.v1.Dapr.DaprClient> Mock { get; }

        public DaprClient DaprClient { get; }

        public InvokeApiCallBuilder<TResponse> Call<TResponse>()
        {
            return new InvokeApiCallBuilder<TResponse>();
        }

        public class InvokeApiCallBuilder<TResponse>
        {
            private TResponse response;
            private readonly Metadata headers;
            private Status status;
            private readonly Metadata trailers;

            public InvokeApiCallBuilder()
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
                    () => { });
            }

            public InvokeApiCallBuilder<TResponse> SetResponse(TResponse response)
            {
                this.response = response;
                return this;
            }

            public InvokeApiCallBuilder<TResponse> SetStatus(Status status)
            {
                this.status = status;
                return this;
            }

            public InvokeApiCallBuilder<TResponse> AddHeader(string key, string value)
            {
                this.headers.Add(key, value);
                return this;
            }

            public InvokeApiCallBuilder<TResponse> AddTrailer(string key, string value)
            {
                this.trailers.Add(key, value);
                return this;
            }
        }
    }
}
