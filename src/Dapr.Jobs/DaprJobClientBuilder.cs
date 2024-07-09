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

using Dapr.Common;
using Grpc.Net.Client;
using Autogenerated = Dapr.Scheduler.Autogen.Grpc.v1.Scheduler;

namespace Dapr.Jobs;

/// <summary>
/// Builds a <see cref="DaprJobsClient"/>.
/// </summary>
public sealed class DaprJobClientBuilder : DaprGenericClientBuilder<DaprJobsClient>
{
    private readonly DaprJobClientOptions options;

    /// <summary>
    /// Used to construct a new instance of <see cref="DaprJobClientBuilder"/>.
    /// </summary>
    /// <param name="options"></param>
    public DaprJobClientBuilder(DaprJobClientOptions options)
    {
        this.options = options;
    }

    /// <summary>
    /// Builds the client instance from the properties of the builder.
    /// </summary>
    /// <returns>The Dapr client instance.</returns>
    public override DaprJobsClient Build()
    {
        var grpcEndpoint = new Uri(this.GrpcEndpoint);
        if (grpcEndpoint.Scheme != "http" && grpcEndpoint.Scheme != "https")
        {
            throw new InvalidOperationException("The gRPC endpoint must use http or https.");
        }

        if (grpcEndpoint.Scheme.Equals(Uri.UriSchemeHttp))
        {
            // Set correct switch to make secure gRPC service calls. This switch must be set before creating the GrpcChannel.
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        }

        var httpEndpoint = new Uri(this.HttpEndpoint);
        if (httpEndpoint.Scheme != "http" && httpEndpoint.Scheme != "https")
        {
            throw new InvalidOperationException("The HTTP endpoint must use http or https.");
        }

        var channel = GrpcChannel.ForAddress(this.GrpcEndpoint, this.GrpcChannelOptions);

        var httpClient = HttpClientFactory is not null ? HttpClientFactory() : new HttpClient();
        if (this.Timeout > TimeSpan.Zero)
        {
            httpClient.Timeout = this.Timeout;
        }

        var client = new Autogenerated.SchedulerClient(channel);
        var apiTokenHeader = DaprJobsClient.GetDaprApiTokenHeader("dapr-api-token");
        
        return new DaprJobsGrpcClient(channel, client, httpClient, httpEndpoint, this.JsonSerializerOptions, apiTokenHeader, this.options);
    }
}
