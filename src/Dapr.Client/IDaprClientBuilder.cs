// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Text.Json;
using Grpc.Net.Client;

namespace Dapr.Client
{
    /// <summary>
    /// Builder for building <see cref="DaprClient"/>
    /// </summary>
    public interface IDaprClientBuilder
    {
        /// <summary>
        /// Builds a <see cref="DaprClient" /> instance from the properties of the builder.
        /// </summary>
        /// <returns>The <see cref="DaprClient" />.</returns>
        DaprClient Build();

        /// <summary>
        /// Adds the provided <paramref name="apiToken" /> on every request to the Dapr runtime.
        /// </summary>
        /// <param name="apiToken">The token to be added to the request headers/>.</param>
        /// <returns>The <see cref="DaprClientBuilder" /> instance.</returns>
        DaprClientBuilder UseDaprApiToken(string apiToken);

        /// <summary>
        /// Uses the provided <paramref name="grpcChannelOptions" /> for creating the <see cref="GrpcChannel" />.
        /// </summary>
        /// <param name="grpcChannelOptions">The <see cref="GrpcChannelOptions" /> to use for creating the <see cref="GrpcChannel" />.</param>
        /// <returns>The <see cref="DaprClientBuilder" /> instance.</returns>
        DaprClientBuilder UseGrpcChannelOptions(GrpcChannelOptions grpcChannelOptions);

        /// <summary>
        /// Overrides the gRPC endpoint used by <see cref="DaprClient" /> for communicating with the Dapr runtime.
        /// </summary>
        /// <param name="grpcEndpoint">
        /// The URI endpoint to use for gRPC calls to the Dapr runtime. The default value will be 
        /// <c>http://127.0.0.1:DAPR_GRPC_PORT</c> where <c>DAPR_GRPC_PORT</c> represents the value of the 
        /// <c>DAPR_GRPC_PORT</c> environment variable.
        /// </param>
        /// <returns>The <see cref="DaprClientBuilder" /> instance.</returns>
        DaprClientBuilder UseGrpcEndpoint(string grpcEndpoint);

        /// <summary>
        /// Overrides the HTTP endpoint used by <see cref="DaprClient" /> for communicating with the Dapr runtime.
        /// </summary>
        /// <param name="httpEndpoint">
        /// The URI endpoint to use for HTTP calls to the Dapr runtime. The default value will be 
        /// <c>http://127.0.0.1:DAPR_HTTP_PORT</c> where <c>DAPR_HTTP_PORT</c> represents the value of the 
        /// <c>DAPR_HTTP_PORT</c> environment variable.
        /// </param>
        /// <returns>The <see cref="DaprClientBuilder" /> instance.</returns>
        DaprClientBuilder UseHttpEndpoint(string httpEndpoint);

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
        DaprClientBuilder UseJsonSerializationOptions(JsonSerializerOptions options);
    }
}
