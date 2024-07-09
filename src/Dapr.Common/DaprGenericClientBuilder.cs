﻿// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

namespace Dapr.Common
{
    using System;
    using System.Net.Http;
    using System.Text.Json;
    using Grpc.Net.Client;

    /// <summary>
    /// Builder for building a generic Dapr client.
    /// </summary>
    public abstract class DaprGenericClientBuilder<TClientBuilder> where TClientBuilder : class
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DaprGenericClientBuilder{TClientBuilder}"/> class.
        /// </summary>
        public DaprGenericClientBuilder()
        {
            this.GrpcEndpoint = DaprDefaults.GetDefaultGrpcEndpoint();
            this.HttpEndpoint = DaprDefaults.GetDefaultHttpEndpoint();

            this.GrpcChannelOptions = new GrpcChannelOptions()
            { 
                // The gRPC client doesn't throw the right exception for cancellation
                // by default, this switches that behavior on.
                ThrowOperationCanceledOnCancellation = true,
            };

            this.JsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            this.DaprApiToken = DaprDefaults.GetDefaultDaprApiToken();
        }

        /// <summary>
        /// Property exposed for testing purposes.
        /// </summary>
        public string GrpcEndpoint { get; private set; }

        /// <summary>
        /// Property exposed for testing purposes.
        /// </summary>
        public string HttpEndpoint { get; private set; }
        
        /// <summary>
        /// Property exposed for testing purposes.
        /// </summary>
        public Func<HttpClient>? HttpClientFactory { get; set; }

        /// <summary>
        /// Property exposed for testing purposes.
        /// </summary>
        public JsonSerializerOptions JsonSerializerOptions { get; private set; }

        /// <summary>
        /// Property exposed for testing purposes.
        /// </summary>
        public GrpcChannelOptions GrpcChannelOptions { get; private set; }
        /// <summary>
        /// Property exposed for testing purposes.
        /// </summary>
        public string DaprApiToken { get; private set; }
        /// <summary>
        /// Property exposed for testing purposes.
        /// </summary>
        public TimeSpan Timeout { get; private set; }

        /// <summary>
        /// Overrides the HTTP endpoint used by the Dapr client for communicating with the Dapr runtime.
        /// </summary>
        /// <param name="httpEndpoint">
        /// The URI endpoint to use for HTTP calls to the Dapr runtime. The default value will be 
        /// <c>DAPR_HTTP_ENDPOINT</c> first, or <c>http://127.0.0.1:DAPR_HTTP_PORT</c> as fallback
        /// where <c>DAPR_HTTP_ENDPOINT</c> and <c>DAPR_HTTP_PORT</c> represents the value of the
        /// corresponding environment variables. 
        /// </param>
        /// <returns>The <see cref="DaprGenericClientBuilder{TClientBuilder}" /> instance.</returns>
        public DaprGenericClientBuilder<TClientBuilder> UseHttpEndpoint(string httpEndpoint)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(httpEndpoint, nameof(httpEndpoint));
            this.HttpEndpoint = httpEndpoint;
            return this;
        }

        /// <summary>
        /// Exposed internally for testing purposes.
        /// </summary>
        internal DaprGenericClientBuilder<TClientBuilder> UseHttpClientFactory(Func<HttpClient> factory)
        {
            this.HttpClientFactory = factory;
            return this;
        }

        /// <summary>
        /// Overrides the gRPC endpoint used by the Dapr client for communicating with the Dapr runtime.
        /// </summary>
        /// <param name="grpcEndpoint">
        /// The URI endpoint to use for gRPC calls to the Dapr runtime. The default value will be 
        /// <c>http://127.0.0.1:DAPR_GRPC_PORT</c> where <c>DAPR_GRPC_PORT</c> represents the value of the 
        /// <c>DAPR_GRPC_PORT</c> environment variable.
        /// </param>
        /// <returns>The <see cref="DaprGenericClientBuilder{TClientBuilder}" /> instance.</returns>
        public DaprGenericClientBuilder<TClientBuilder> UseGrpcEndpoint(string grpcEndpoint)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(grpcEndpoint, nameof(grpcEndpoint));
            this.GrpcEndpoint = grpcEndpoint;
            return this;
        }

        /// <summary>
        /// <para>
        /// Uses the specified <see cref="JsonSerializerOptions"/> when serializing or deserializing using <see cref="System.Text.Json"/>.
        /// </para>
        /// <para>
        /// The default value is created using <see cref="JsonSerializerDefaults.Web" />.
        /// </para>
        /// </summary>
        /// <param name="options">Json serialization options.</param>
        /// <returns>The <see cref="DaprGenericClientBuilder{TClientBuilder}" /> instance.</returns>
        public DaprGenericClientBuilder<TClientBuilder> UseJsonSerializationOptions(JsonSerializerOptions options)
        {
            this.JsonSerializerOptions = options;
            return this;
        }

        /// <summary>
        /// Uses the provided <paramref name="grpcChannelOptions" /> for creating the <see cref="GrpcChannel" />.
        /// </summary>
        /// <param name="grpcChannelOptions">The <see cref="GrpcChannelOptions" /> to use for creating the <see cref="GrpcChannel" />.</param>
        /// <returns>The <see cref="DaprGenericClientBuilder{TClientBuilder}" /> instance.</returns>
        public DaprGenericClientBuilder<TClientBuilder> UseGrpcChannelOptions(GrpcChannelOptions grpcChannelOptions)
        {
            this.GrpcChannelOptions = grpcChannelOptions;
            return this;
        }

        /// <summary>
        /// Adds the provided <paramref name="apiToken" /> on every request to the Dapr runtime.
        /// </summary>
        /// <param name="apiToken">The token to be added to the request headers/>.</param>
        /// <returns>The <see cref="DaprGenericClientBuilder{TClientBuilder}" /> instance.</returns>
        public DaprGenericClientBuilder<TClientBuilder> UseDaprApiToken(string apiToken)
        {
            this.DaprApiToken = apiToken;
            return this;
        }

        /// <summary>
        ///  Sets the timeout for the HTTP client used by the Dapr client.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public DaprGenericClientBuilder<TClientBuilder> UseTimeout(TimeSpan timeout)
        {
            this.Timeout = timeout;
            return this;
        }

        /// <summary>
        /// Builds the client instance from the properties of the builder.
        /// </summary>
        /// <returns>The Dapr client instance.</returns>
        public abstract TClientBuilder Build();
    }
}