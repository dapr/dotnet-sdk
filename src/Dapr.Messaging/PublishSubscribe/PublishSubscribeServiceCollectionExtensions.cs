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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dapr.Messaging.PublishSubscribe;

/// <summary>
/// Contains extension methods for using Dapr Publish/Subscribe with dependency injection.
/// </summary>
public static class PublishSubscribeServiceCollectionExtensions
{
    public static IServiceCollection AddDaprPubSub(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
    }

    public static IServiceCollection AddDaprPubSub(this IServiceCollection services, Action<IServiceProvider> configure)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        services.TryAddSingleton(serviceProvider =>
        {
            
        });
    }
}
