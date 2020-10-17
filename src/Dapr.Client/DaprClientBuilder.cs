// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    using System;
    using Grpc.Net.Client;

    /// <summary>
    /// Builder for building <see cref="DaprClient"/>
    /// </summary>
    public sealed class DaprClientBuilder
    {
        private const string DefaultDaprGrpcPort = "50001";

        // property exposed for testing purposes
        internal GrpcSerializer GrpcSerializer { get; }

        // property exposed for testing purposes
        internal string DaprEndpoint { get; private set; }

        // property exposed for testing purposes
        internal GrpcChannelOptions GrpcChannelOptions { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DaprClientBuilder"/> class.
        /// </summary>
        public DaprClientBuilder(): this(new GrpcSerializer())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DaprClientBuilder"/> class.
        /// </summary>
        /// <param name="serializer">The gRPC serializer instance to use.</param>
        public DaprClientBuilder(GrpcSerializer serializer)
        {
            var daprGrpcPort = Environment.GetEnvironmentVariable("DAPR_GRPC_PORT") ?? DefaultDaprGrpcPort;
            this.DaprEndpoint = $"http://127.0.0.1:{daprGrpcPort}";
            this.GrpcSerializer = serializer;
        }

        /// <summary>
        /// Overrides the default endpoint used by IDaprClient for connecting to Dapr runtime.
        /// </summary>
        /// <param name="daprEndpoint">Endpoint to use for making calls to Dapr runtime.
        /// Default endpoint used is http://127.0.0.1:DAPR_GRPC_PORT.</param>
        /// <returns>DaprClientBuilder instance.</returns>
        public DaprClientBuilder UseEndpoint(string daprEndpoint)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(daprEndpoint, nameof(daprEndpoint));
            this.DaprEndpoint = daprEndpoint;
            return this;
        }

        /// <summary>
        /// Uses options for configuring a Grpc.Net.Client.GrpcChannel.
        /// </summary>
        /// <param name="grpcChannelOptions"></param>
        /// <returns></returns>
        public DaprClientBuilder UseGrpcChannelOptions(GrpcChannelOptions grpcChannelOptions)
        {
            this.GrpcChannelOptions = grpcChannelOptions;
            return this;
        }

        /// <summary>
        /// Builds a DaprClient.
        /// </summary>
        /// <returns>A DaprClient instance.</returns>
        public DaprClient Build()
        {
            var uri = new Uri(this.DaprEndpoint);
            if (uri.Scheme.Equals(Uri.UriSchemeHttp))
            {
                // Set correct switch to make insecure gRPC service calls. This switch must be set before creating the GrpcChannel.
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            }

            var channel = GrpcChannel.ForAddress(this.DaprEndpoint, this.GrpcChannelOptions ?? new GrpcChannelOptions());
            return new DaprClientGrpc(channel, this.GrpcSerializer);
        }
    }
}
