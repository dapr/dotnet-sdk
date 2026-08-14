// ------------------------------------------------------------------------
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

using Dapr.Common.Extensions;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dapr.Jobs.Extensions;

/// <summary>
/// Contains extension methods for using Dapr Jobs with dependency injection.
/// </summary>
public static class DaprJobsServiceCollectionExtensions
{
    /// <summary>
    /// Adds Dapr Jobs client support to the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configure">Optionally allows greater configuration of the <see cref="DaprJobsClient"/> using injected services.</param>
    /// <param name="lifetime">The lifetime of the registered services.</param>
    /// <returns></returns>
    public static IDaprJobsBuilder AddDaprJobsClient(
        this IServiceCollection services,
        Action<IServiceProvider, DaprJobsClientBuilder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        // Register gRPC server infrastructure so MapDaprScheduledJobHandler can map the
        // AppCallbackAlpha service for gRPC callbacks from the Dapr runtime.
        // AddGrpc uses TryAdd internally, so this is safe even if the user already called it.
        services.AddGrpc();

        // Configure Kestrel to accept both HTTP/1.x and HTTP/2 on the same port.
        // HTTP/2 is required for gRPC over plaintext (used by the sidecar when --app-protocol grpc
        // is set), while HTTP/1.x continues to serve the HTTP callback endpoint and any other
        // middleware the app registers. This is backward-compatible with the default Kestrel
        // configuration and uses OptionsConfiguration so a user's own Kestrel configuration
        // applied later still takes precedence.
        services.Configure<KestrelServerOptions>(options =>
        {
            options.ConfigureEndpointDefaults(listen => listen.Protocols = HttpProtocols.Http1AndHttp2);
        });

        // The handler registry holds the user-supplied delegate and timeout; it is populated
        // by MapDaprScheduledJobHandler at endpoint configuration time and consumed by the
        // gRPC callback service at runtime.
        services.TryAddSingleton<DaprJobsHandlerRegistry>();

        // The AppCallbackAlpha service is resolved per-request by MapGrpcService.
        services.TryAddTransient<DaprJobsAppCallbackService>();

        return services.AddDaprClient<DaprJobsClient, DaprJobsGrpcClient, DaprJobsBuilder, DaprJobsClientBuilder>(
            config => new DaprJobsClientBuilder(config),
            svc => new DaprJobsBuilder(svc),
            configure,
            lifetime);
    }
}
