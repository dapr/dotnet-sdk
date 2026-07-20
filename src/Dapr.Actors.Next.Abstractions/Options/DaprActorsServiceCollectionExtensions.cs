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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dapr.Actors.Next.Abstractions.Options;

/// <summary>
/// Registers Dapr Actors Next options. Actor lifetime is runtime activation lifetime and is not configurable as a DI lifetime.
/// </summary>
public static class DaprActorsServiceCollectionExtensions
{
    /// <summary>
    /// Adds Dapr Actors Next services.
    /// </summary>
    public static IServiceCollection AddDaprActors(this IServiceCollection services, Action<DaprActorsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        configure ??= _ => { };

        var configuredOptions = new DaprActorsOptions();
        configure(configuredOptions);

        services.AddOptions<DaprActorsOptions>().Configure(options => options.CopyFrom(configuredOptions));
        services.AddSingleton<IValidateOptions<DaprActorsOptions>, DaprActorsOptionsValidator>();
        DaprActorsGeneratedRegistration.Apply(services, configuredOptions);
        return services;
    }
}
