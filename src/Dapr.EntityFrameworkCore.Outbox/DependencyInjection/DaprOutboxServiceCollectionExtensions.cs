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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dapr.EntityFrameworkCore.Outbox.DependencyInjection;

/// <summary>
/// Extension methods for registering the Dapr EF Core outbox with a service collection.
/// </summary>
public static class DaprOutboxServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Dapr outbox services scoped to <typeparamref name="TDbContext"/>.
    /// The caller is responsible for attaching <see cref="DaprOutboxSaveChangesInterceptor"/>
    /// to the <see cref="DbContext"/> via <c>optionsBuilder.AddInterceptors(...)</c>.
    /// </summary>
    /// <typeparam name="TDbContext">The <see cref="DbContext"/> type the outbox is bound to.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional callback to configure <see cref="DaprOutboxOptions"/>.</param>
    /// <returns>An <see cref="IDaprOutboxBuilder"/> for further chaining.</returns>
    public static IDaprOutboxBuilder AddDaprOutbox<TDbContext>(
        this IServiceCollection services,
        Action<DaprOutboxOptions>? configure = null)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<DaprOutboxOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IOutboxPendingBuffer, OutboxPendingBuffer>();
        services.TryAddSingleton<IOutboxMessageFactory, AttributeOutboxMessageFactory>();

        // Interceptor is scoped so it flows with the DbContext scope and can safely
        // resolve scoped services in later phases (e.g., loggers with request context).
        services.AddScoped<DaprOutboxSaveChangesInterceptor>();

        return new DaprOutboxBuilder(services, typeof(TDbContext));
    }
}
