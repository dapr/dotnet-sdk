﻿// ------------------------------------------------------------------------
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

#nullable enable

namespace Microsoft.Extensions.DependencyInjection;

using System;
using Dapr;
using Dapr.Client;
using Extensions;
using Configuration;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection" />.
/// </summary>
public static class DaprServiceCollectionExtensions
{
    /// <summary>
    /// Adds Dapr client services to the provided <see cref="IServiceCollection" />. This does not include integration
    /// with ASP.NET Core MVC. Use the <c>AddDapr()</c> extension method on <c>IMvcBuilder</c> to register MVC integration.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" />.</param>
    /// <param name="configure"></param>
    public static void AddDaprClient(this IServiceCollection services, Action<DaprClientBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        services.TryAddSingleton(serviceProvider =>
        {
            var builder = CreateDaprClientBuilder(serviceProvider);
            configure?.Invoke(builder);
            return builder.Build();
        });
    }

    /// <summary>
    /// Adds Dapr client services to the provided <see cref="IServiceCollection"/>. This does not include integration
    /// with ASP.NET Core MVC. Use the <c>AddDapr()</c> extension method on <c>IMvcBuilder</c> to register MVC integration. 
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configure"></param>
    public static void AddDaprClient(this IServiceCollection services,
        Action<IServiceProvider, DaprClientBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        services.TryAddSingleton(serviceProvider =>
        {
            var builder = CreateDaprClientBuilder(serviceProvider);
            configure?.Invoke(serviceProvider, builder);
            return builder.Build();
        });
    }
    
    private static DaprClientBuilder CreateDaprClientBuilder(IServiceProvider serviceProvider)
    {
        var builder = new DaprClientBuilder();
        var configuration = serviceProvider.GetService<IConfiguration>();

        // Set the HTTP endpoint, if provided, else use the default endpoint
        builder.UseHttpEndpoint(DaprDefaults.GetDefaultHttpEndpoint(configuration));

        // Set the gRPC endpoint, if provided
        builder.UseGrpcEndpoint(DaprDefaults.GetDefaultGrpcEndpoint(configuration));

        // Set the API token, if provided
        var apiToken = DaprDefaults.GetDefaultDaprApiToken(configuration);
        if (!string.IsNullOrWhiteSpace(apiToken))
        {
            builder.UseDaprApiToken(apiToken);
        }

        return builder;
    }
}
