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

        public PublishApiCallBuilder Publish()
        {
            return new PublishApiCallBuilder();
        }

        public InvokeBindingApiCallBuilder<TResponse> InvokeBinding<TResponse>()
        {
            return new InvokeBindingApiCallBuilder<TResponse>();
        }

        public StateApiCallBuilder<TResponse> SetState<TResponse>()
        {
            return new StateApiCallBuilder<TResponse>();
        }

        public SecretsApiCallBuilder<TResponse> SetSecrets<TResponse>()
        {
            return new SecretsApiCallBuilder<TResponse>();
        }

        public class InvokeApiCallBuilder<TResponse>
        {
            private TResponse response;
            private Metadata headers;
            private Status status;
            private Metadata trailers;

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

    public class PublishApiCallBuilder
    {
        private global::Google.Protobuf.WellKnownTypes.Empty response;
        private Metadata headers;
        private Status status;
        private Metadata trailers;

        public PublishApiCallBuilder()
        {
            headers = new Metadata();
            trailers = new Metadata();
        }

        public AsyncUnaryCall<global::Google.Protobuf.WellKnownTypes.Empty> Build()
        {
            return new AsyncUnaryCall<global::Google.Protobuf.WellKnownTypes.Empty>(
                Task.FromResult(response),
                Task.FromResult(headers),
                () => status,
                () => trailers,
                () => { });
        }

        public PublishApiCallBuilder SetResponse()
        {
            this.response = null;
            return this;
        }

        public PublishApiCallBuilder SetStatus(Status status)
        {
            this.status = status;
            return this;
        }
    }

    public class InvokeBindingApiCallBuilder<TResponse>
    {
        private TResponse response;
        private Metadata headers;
        private Status status;
        private Metadata trailers;

        public InvokeBindingApiCallBuilder()
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

        public InvokeBindingApiCallBuilder<TResponse> SetResponse(TResponse response)
        {
            this.response = response;
            return this;
        }

        public InvokeBindingApiCallBuilder<TResponse> SetStatus(Status status)
        {
            this.status = status;
            return this;
        }

        public InvokeBindingApiCallBuilder<TResponse> AddHeader(string key, string value)
        {
            this.headers.Add(key, value);
            return this;
        }

        public InvokeBindingApiCallBuilder<TResponse> AddTrailer(string key, string value)
        {
            this.trailers.Add(key, value);
            return this;
        }
    }

    public class StateApiCallBuilder<TResponse>
    {
        private TResponse response;
        private Metadata headers;
        private Status status;
        private Metadata trailers;

        public StateApiCallBuilder()
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

        public StateApiCallBuilder<TResponse> SetResponse(TResponse response)
        {
            this.response = response;
            return this;
        }

        public StateApiCallBuilder<TResponse> SetStatus(Status status)
        {
            this.status = status;
            return this;
        }

        public StateApiCallBuilder<TResponse> AddHeader(string key, string value)
        {
            this.headers.Add(key, value);
            return this;
        }

        public StateApiCallBuilder<TResponse> AddTrailer(string key, string value)
        {
            this.trailers.Add(key, value);
            return this;
        }
    }

    public class SecretsApiCallBuilder<TResponse>
    {
        private TResponse response;
        private Metadata headers;
        private Status status;
        private Metadata trailers;

        public SecretsApiCallBuilder()
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

        public SecretsApiCallBuilder<TResponse> SetResponse(TResponse response)
        {
            this.response = response;
            return this;
        }

        public SecretsApiCallBuilder<TResponse> SetStatus(Status status)
        {
            this.status = status;
            return this;
        }

        public SecretsApiCallBuilder<TResponse> AddHeader(string key, string value)
        {
            this.headers.Add(key, value);
            return this;
        }

        public SecretsApiCallBuilder<TResponse> AddTrailer(string key, string value)
        {
            this.trailers.Add(key, value);
            return this;
        }
    }
}