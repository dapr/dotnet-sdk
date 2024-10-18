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

namespace Dapr.Client
{
    using System;
    using System.Net.Http;
    using System.Text.Json;
    using Grpc.Net.Client;
    using Autogenerated = Autogen.Grpc.v1;

    /// <summary>
    /// Builder for building <see cref="DaprClient"/>
    /// </summary>
    public sealed class DaprClientBuilder
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DaprClientBuilder"/> class.
        /// </summary>
        public DaprClientBuilder()
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
            this.DaprApiToken = DaprDefaults.GetDefaultDaprApiToken(null);
        }

        // property exposed for testing purposes
        internal string GrpcEndpoint { get; private set; }

        // property exposed for testing purposes
        internal string HttpEndpoint { get; private set; }

        private Func<HttpClient> HttpClientFactory { get; set; }

        // property exposed for testing purposes
        internal JsonSerializerOptions JsonSerializerOptions { get; private set; }

        // property exposed for testing purposes
        internal GrpcChannelOptions GrpcChannelOptions { get; private set; }
        internal string DaprApiToken { get; private set; }
        internal TimeSpan Timeout { get; private set; } 

        /// <summary>
        /// Overrides the HTTP endpoint used by <see cref="DaprClient" /> for communicating with the Dapr runtime.
        /// </summary>
        /// <param name="httpEndpoint">
        /// The URI endpoint to use for HTTP calls to the Dapr runtime. The default value will be 
        /// <c>DAPR_HTTP_ENDPOINT</c> first, or <c>http://127.0.0.1:DAPR_HTTP_PORT</c> as fallback
        /// where <c>DAPR_HTTP_ENDPOINT</c> and <c>DAPR_HTTP_PORT</c> represents the value of the
        /// corresponding environment variables. 
        /// </param>
        /// <returns>The <see cref="DaprClientBuilder" /> instance.</returns>
        public DaprClientBuilder UseHttpEndpoint(string httpEndpoint)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(httpEndpoint, nameof(httpEndpoint));
            this.HttpEndpoint = httpEndpoint;
            return this;
        }

        // Internal for testing of DaprClient
        internal DaprClientBuilder UseHttpClientFactory(Func<HttpClient> factory)
        {
            this.HttpClientFactory = factory;
            return this;
        }

        /// <summary>
        /// Overrides the gRPC endpoint used by <see cref="DaprClient" /> for communicating with the Dapr runtime.
        /// </summary>
        /// <param name="grpcEndpoint">
        /// The URI endpoint to use for gRPC calls to the Dapr runtime. The default value will be 
        /// <c>http://127.0.0.1:DAPR_GRPC_PORT</c> where <c>DAPR_GRPC_PORT</c> represents the value of the 
        /// <c>DAPR_GRPC_PORT</c> environment variable.
        /// </param>
        /// <returns>The <see cref="DaprClientBuilder" /> instance.</returns>
        public DaprClientBuilder UseGrpcEndpoint(string grpcEndpoint)
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
        /// <returns>The <see cref="DaprClientBuilder" /> instance.</returns>
        public DaprClientBuilder UseJsonSerializationOptions(JsonSerializerOptions options)
        {
            this.JsonSerializerOptions = options;
            return this;
        }

        /// <summary>
        /// Uses the provided <paramref name="grpcChannelOptions" /> for creating the <see cref="GrpcChannel" />.
        /// </summary>
        /// <param name="grpcChannelOptions">The <see cref="GrpcChannelOptions" /> to use for creating the <see cref="GrpcChannel" />.</param>
        /// <returns>The <see cref="DaprClientBuilder" /> instance.</returns>
        public DaprClientBuilder UseGrpcChannelOptions(GrpcChannelOptions grpcChannelOptions)
        {
            this.GrpcChannelOptions = grpcChannelOptions;
            return this;
        }

        /// <summary>
        /// Adds the provided <paramref name="apiToken" /> on every request to the Dapr runtime.
        /// </summary>
        /// <param name="apiToken">The token to be added to the request headers/>.</param>
        /// <returns>The <see cref="DaprClientBuilder" /> instance.</returns>
        public DaprClientBuilder UseDaprApiToken(string apiToken)
        {
            this.DaprApiToken = apiToken;
            return this;
        }

        /// <summary>
        ///  Sets the timeout for the HTTP client used by the <see cref="DaprClient" />.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public DaprClientBuilder UseTimeout(TimeSpan timeout)
        {
            this.Timeout = timeout;
            return this;
        }

        /// <summary>
        /// Builds a <see cref="DaprClient" /> instance from the properties of the builder.
        /// </summary>
        /// <returns>The <see cref="DaprClient" />.</returns>
        public DaprClient Build()
        {
            var grpcEndpoint = new Uri(this.GrpcEndpoint);
            if (grpcEndpoint.Scheme != "http" && grpcEndpoint.Scheme != "https")
            {
                throw new InvalidOperationException("The gRPC endpoint must use http or https.");
            }

            if (grpcEndpoint.Scheme.Equals(Uri.UriSchemeHttp))
            {
                // Set correct switch to maksecure gRPC service calls. This switch must be set before creating the GrpcChannel.
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            }

            var httpEndpoint = new Uri(this.HttpEndpoint);
            if (httpEndpoint.Scheme != "http" && httpEndpoint.Scheme != "https")
            {
                throw new InvalidOperationException("The HTTP endpoint must use http or https.");
            }

            var channel = GrpcChannel.ForAddress(this.GrpcEndpoint, this.GrpcChannelOptions);
            var client = new Autogenerated.Dapr.DaprClient(channel);


            var apiTokenHeader = DaprClient.GetDaprApiTokenHeader(this.DaprApiToken);
            var httpClient = HttpClientFactory is object ? HttpClientFactory() : new HttpClient();
            
            if (this.Timeout > TimeSpan.Zero)
            {
                httpClient.Timeout = this.Timeout;
            }

            return new DaprClientGrpc(channel, client, httpClient, httpEndpoint, this.JsonSerializerOptions, apiTokenHeader);
        }
    }
}
