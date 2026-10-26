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

namespace Dapr.Search.Extensions;

/// <summary>Dependency injection registration for Dapr Search.</summary>
public static class DaprSearchServiceCollectionExtensions
{
    /// <summary>Registers a Search client. Singleton lifetime is recommended for shared network resources.</summary>
    public static IDaprSearchBuilder AddDaprSearchClient(this IServiceCollection services,
        Action<IServiceProvider, DaprSearchClientBuilder>? configure = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton) =>
        services.AddDaprClient<DaprSearchClient, DaprSearchGrpcClient, DaprSearchBuilder, DaprSearchClientBuilder>(
            config => new DaprSearchClientBuilder(config), svc => new DaprSearchBuilder(svc), configure, lifetime);
}
