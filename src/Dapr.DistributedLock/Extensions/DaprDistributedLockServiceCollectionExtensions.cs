// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using Dapr.Common.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.DistributedLock.Extensions;

/// <summary>
/// Contains extension methods for using Dapr distributed lock with dependency injection.
/// </summary>
[Experimental("DAPR_DISTRIBUTEDLOCK", UrlFormat = "https://docs.dapr.io/developing-applications/building-blocks/distributed-lock/distributed-lock-api-overview/")]
public static class DaprDistributedLockServiceCollectionExtensions
{
    /// <summary>
    /// Adds Dapr distributed lock support to the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <param name="configure">Optionally allows greater configuration of the <see cref="DaprDistributedLockBuilder"/>.</param>
    /// <param name="lifetime">The lifetime of the registered services.</param>
    /// <returns></returns>
    public static IDaprDistributedLockBuilder AddDaprDistributedLock(
        this IServiceCollection services,
        Action<IServiceProvider, DaprDistributedLockBuilder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped) =>
        services.AddDaprClient<DaprDistributedLockClient, DaprDistributedLockGrpcClient, DaprLockBuilder, DaprDistributedLockBuilder>(configure, lifetime);
}
