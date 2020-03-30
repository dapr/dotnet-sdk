﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    using System;
    using System.Text.Json;
    using Grpc.Net.Client;

    /// <summary>
    /// Builder for building <see cref="DaprClient"/>
    /// </summary>
    public sealed class DaprClientBuilder
    {
        const string defaultDaprGrpcPort = "50001";
        string daprEndpoint;
        JsonSerializerOptions jsonSerializerOptions;
        GrpcChannelOptions gRPCChannelOptions;


        /// <summary>
        /// Initializes a new instance of the <see cref="DaprClientBuilder"/> class.
        /// </summary>
        public DaprClientBuilder()
        {
            var daprGrpcPort = Environment.GetEnvironmentVariable("DAPR_GRPC_PORT") ?? defaultDaprGrpcPort;
            this.daprEndpoint = $"http://127.0.0.1:{daprGrpcPort}";
        }

        /// <summary>
        /// Overrides the default endpoint used by IDaprClient for conencting to Dapr runtime.
        /// </summary>
        /// <param name="daprEndpoint">Endpoint to use for making calls to Dapr runtime. 
        /// Default endpoint used is http://127.0.0.1:DAPR_GRPC_PORT.</param>
        /// <returns>DaprClientBuilder instance.</returns>
        public DaprClientBuilder UseEndpoint(string daprEndpoint)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(daprEndpoint, nameof(daprEndpoint));
            this.daprEndpoint = daprEndpoint;
            return this;
        }

        /// <summary>
        /// Uses the specified <see cref="JsonSerializerOptions"/> when serializing or deserializing using <see cref="System.Text.Json"/>.
        /// </summary>
        /// <param name="options">Json serialization options.</param>
        /// <returns>DaprClientBuilder instance.</returns>
        public DaprClientBuilder UseJsonSerializationOptions(JsonSerializerOptions options)
        {
            this.jsonSerializerOptions = options;
            return this;
        }

        /// <summary>
        /// Builds a DaprClient.
        /// </summary>
        /// <returns>A DaprClient isntance.</returns>
        public DaprClient Build()
        {
            var uri = new Uri(this.daprEndpoint);
            if (uri.Scheme.Equals(Uri.UriSchemeHttp))
            {
                // Set correct switch to make insecure gRPC service calls. This switch must be set before creating the GrpcChannel.
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            }

            var channel = GrpcChannel.ForAddress(this.daprEndpoint, this.gRPCChannelOptions ?? new GrpcChannelOptions());
            return new DaprClientGrpc(channel, this.jsonSerializerOptions);
        }

        /// <summary>
        /// Usees options for configuring a Grpc.Net.Client.GrpcChannel.
        /// </summary>
        /// <param name="gRPCChannelOptions"></param>
        /// <returns></returns>
        public DaprClientBuilder UseGrpcChannelOptions(GrpcChannelOptions gRPCChannelOptions)
        {
            this.gRPCChannelOptions = gRPCChannelOptions;
            return this;
        }
    }
}
