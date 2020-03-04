// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    using System;
    using System.Text.Json;
    using Grpc.Net.Client;

    /// <summary>
    /// Builder for building IDaprClient
    /// </summary>
    public class DaprClientBuilder
    {
        string daprEndpoint;
        JsonSerializerOptions jsonSerializerOptions;

        /// <summary>
        /// 
        /// </summary>
        public DaprClientBuilder()
        {
            var defaultPort = Environment.GetEnvironmentVariable("DAPR_GRPC_PORT") ?? "52918";
            var daprEndpoint = $"http://127.0.0.1:{defaultPort}";
        }

        /// <summary>
        /// Overrides the default endpoint used by IDaprClient for conencting to Dapr runtime.
        /// </summary>
        /// <param name="daprEndpoint">Endpoint to use for making calls to Dapr runtime. 
        /// Default endpoint used is http://127.0.0.1:DAPR_GRPC_PORT.</param>
        /// <returns>DaprClientBuilder instance.</returns>
        public DaprClientBuilder UseEndpoint(string daprEndpoint)
        {
            this.daprEndpoint = daprEndpoint;
            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public DaprClientBuilder UseJsonSerializationOptions(JsonSerializerOptions options)
        {
            this.jsonSerializerOptions = options;
            return this;
        }

        /// <summary>
        /// Builds a DaprClient.
        /// </summary>
        /// <returns>A DaprClient isntance.</returns>
        public IDaprClient Build()
        {
            var uri = new Uri(daprEndpoint);
            if (uri.Scheme.Equals(Uri.UriSchemeHttp))
            {
                // Set correct switch to make insecure gRPC service calls. This switch must be set before creating the GrpcChannel.
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            }

            var channel = GrpcChannel.ForAddress(daprEndpoint);
            return new DaprClient(channel, this.jsonSerializerOptions);
        }
    }
}
