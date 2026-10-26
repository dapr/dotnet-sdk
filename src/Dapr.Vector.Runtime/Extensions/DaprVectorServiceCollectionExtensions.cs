// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Vector.Extensions;

/// <summary>Dependency injection registration for Dapr Vector.</summary>
public static class DaprVectorServiceCollectionExtensions
{
    /// <summary>Registers a Vector client. Singleton lifetime is recommended for shared network resources.</summary>
    public static IDaprVectorBuilder AddDaprVectorClient(this IServiceCollection services,
        Action<IServiceProvider, DaprVectorClientBuilder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton) =>
        services.AddDaprClient<DaprVectorClient, DaprVectorGrpcClient, DaprVectorBuilder, DaprVectorClientBuilder>(
            config => new DaprVectorClientBuilder(config), svc => new DaprVectorBuilder(svc), configure, lifetime);
}
