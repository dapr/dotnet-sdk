// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr.Client;

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using Moq;

public class MockClient
{
    public MockClient()
    {
        Mock = new Mock<Autogen.Grpc.v1.Dapr.DaprClient>(MockBehavior.Strict);
        DaprClient = new DaprClientGrpc(GrpcChannel.ForAddress("http://localhost"), Mock.Object, new HttpClient(), new Uri("http://localhost:3500"), new JsonSerializerOptions(), default);
    }

    public Mock<Autogen.Grpc.v1.Dapr.DaprClient> Mock { get; }

    public DaprClient DaprClient { get; }

    public InvokeApiCallBuilder<TResponse> Call<TResponse>()
    {
        return new InvokeApiCallBuilder<TResponse>();
    }

    public StateApiCallBuilder<TResponse> CallStateApi<TResponse>()
    {
        return new StateApiCallBuilder<TResponse>();
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

    public class StateApiCallBuilder<TResponse>
    {
        private TResponse response;
        private readonly Metadata headers;
        private Status status;
        private readonly Metadata trailers;

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
}