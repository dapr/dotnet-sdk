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

#nullable enable

using System;
using Dapr.EntityFrameworkCore.Outbox.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dapr.EntityFrameworkCore.Outbox.DependencyInjection;

/// <summary>
/// Fluent extension methods on <see cref="IDaprOutboxBuilder"/> for wiring the dispatcher,
/// claim strategy, hosted service, and health check.
/// </summary>
public static class DaprOutboxBuilderExtensions
{
    /// <summary>
    /// Replaces the default <see cref="IOutboxMessageFactory"/> registration with <typeparamref name="TFactory"/>.
    /// </summary>
    public static IDaprOutboxBuilder UseMessageFactory<TFactory>(this IDaprOutboxBuilder builder)
        where TFactory : class, IOutboxMessageFactory
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.RemoveAll<IOutboxMessageFactory>();
        builder.Services.AddSingleton<IOutboxMessageFactory, TFactory>();
        return builder;
    }

    /// <summary>
    /// Replaces the default <see cref="IOutboxDispatcher"/> registration with <typeparamref name="TDispatcher"/>.
    /// </summary>
    public static IDaprOutboxBuilder UseDispatcher<TDispatcher>(this IDaprOutboxBuilder builder)
        where TDispatcher : class, IOutboxDispatcher
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.RemoveAll<IOutboxDispatcher>();
        builder.Services.AddSingleton<IOutboxDispatcher, TDispatcher>();
        return builder;
    }

    /// <summary>
    /// Replaces the default <see cref="IOutboxClaimStrategy"/> registration with <typeparamref name="TStrategy"/>.
    /// </summary>
    public static IDaprOutboxBuilder UseClaimStrategy<TStrategy>(this IDaprOutboxBuilder builder)
        where TStrategy : class, IOutboxClaimStrategy
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.RemoveAll<IOutboxClaimStrategy>();
        builder.Services.AddScoped<IOutboxClaimStrategy, TStrategy>();
        return builder;
    }

    /// <summary>
    /// Registers the default polling dispatcher, portable relational claim strategy,
    /// and background hosted service. Idempotent (multiple calls have no additional effect).
    /// </summary>
    public static IDaprOutboxBuilder AddDefaultDispatcher(this IDaprOutboxBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddScoped<IOutboxClaimStrategy, RelationalOutboxClaimStrategy>();

        var dbContextType = builder.DbContextType;
        var dispatcherServiceType = typeof(IOutboxDispatcher);
        var dispatcherImplType = typeof(PollingOutboxDispatcher<>).MakeGenericType(dbContextType);
        var hostedServiceType = typeof(DaprOutboxHostedService<>).MakeGenericType(dbContextType);

        builder.Services.TryAddSingleton(dispatcherServiceType, dispatcherImplType);
        builder.Services.AddSingleton(typeof(Microsoft.Extensions.Hosting.IHostedService), hostedServiceType);

        return builder;
    }

    /// <summary>
    /// Registers <see cref="SqlServerOutboxClaimStrategy"/> in place of the default portable strategy.
    /// </summary>
    public static IDaprOutboxBuilder AddSqlServerClaimStrategy(this IDaprOutboxBuilder builder)
        => builder.UseClaimStrategy<SqlServerOutboxClaimStrategy>();

    /// <summary>
    /// Registers <see cref="PostgreSqlOutboxClaimStrategy"/> in place of the default portable strategy.
    /// Uses <c>SELECT ... FOR UPDATE SKIP LOCKED</c> for concurrent-safe non-blocking claims.
    /// </summary>
    public static IDaprOutboxBuilder AddPostgreSqlClaimStrategy(this IDaprOutboxBuilder builder)
        => builder.UseClaimStrategy<PostgreSqlOutboxClaimStrategy>();

    /// <summary>
    /// Registers <see cref="OutboxRetentionHostedService{TDbContext}"/> to periodically delete
    /// successfully processed outbox rows older than <see cref="DaprOutboxOptions.RetentionPeriod"/>.
    /// Only rows with <c>ProcessedAt IS NOT NULL</c> are eligible; dead-lettered and pending rows
    /// are preserved for operator inspection.
    /// </summary>
    public static IDaprOutboxBuilder AddRetentionService(this IDaprOutboxBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var dbContextType = builder.DbContextType;
        var hostedServiceType = typeof(OutboxRetentionHostedService<>).MakeGenericType(dbContextType);
        builder.Services.AddSingleton(typeof(Microsoft.Extensions.Hosting.IHostedService), hostedServiceType);

        return builder;
    }

    /// <summary>
    /// Adds an <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck"/> for the outbox
    /// under <paramref name="name"/> (default <c>"dapr-outbox"</c>).
    /// </summary>
    public static IDaprOutboxBuilder AddOutboxHealthCheck(
        this IDaprOutboxBuilder builder,
        string name = "dapr-outbox",
        HealthStatus? failureStatus = null,
        params string[] tags)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var dbContextType = builder.DbContextType;
        var healthCheckType = typeof(DaprOutboxHealthCheck<>).MakeGenericType(dbContextType);

        builder.Services.AddSingleton(healthCheckType);
        builder.Services.AddHealthChecks().Add(new HealthCheckRegistration(
            name,
            sp => (IHealthCheck)sp.GetRequiredService(healthCheckType),
            failureStatus,
            tags));

        return builder;
    }
}
