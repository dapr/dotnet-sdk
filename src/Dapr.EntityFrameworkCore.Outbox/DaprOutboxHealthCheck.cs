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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Dapr.EntityFrameworkCore.Outbox;

/// <summary>
/// Reports the outbox as Unhealthy when the oldest unprocessed row's <c>OccurredAt</c>
/// exceeds <see cref="DaprOutboxOptions.HealthCheckThreshold"/>, and Degraded when the
/// oldest is between half the threshold and the full threshold. Returns Healthy when no
/// unprocessed rows exist, no threshold is configured, or lag is under half the threshold.
/// </summary>
/// <typeparam name="TDbContext">The application <see cref="DbContext"/> containing the outbox table.</typeparam>
public sealed class DaprOutboxHealthCheck<TDbContext> : IHealthCheck
    where TDbContext : DbContext
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IOptions<DaprOutboxOptions> options;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Creates the health check.
    /// </summary>
    public DaprOutboxHealthCheck(
        IServiceScopeFactory scopeFactory,
        IOptions<DaprOutboxOptions> options,
        TimeProvider timeProvider)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        if (opts.HealthCheckThreshold is not TimeSpan threshold)
        {
            return HealthCheckResult.Healthy("Outbox health check disabled (no HealthCheckThreshold configured).");
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();

        var oldest = await db.Set<OutboxMessage>()
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.OccurredAt)
            .Select(m => (DateTimeOffset?)m.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (oldest is null)
        {
            return HealthCheckResult.Healthy("Outbox has no unprocessed messages.");
        }

        var lag = timeProvider.GetUtcNow() - oldest.Value;
        var data = new Dictionary<string, object>
        {
            ["oldestUnprocessedOccurredAt"] = oldest.Value,
            ["lagSeconds"] = lag.TotalSeconds,
            ["thresholdSeconds"] = threshold.TotalSeconds,
        };

        if (lag >= threshold)
        {
            return HealthCheckResult.Unhealthy(
                $"Oldest unprocessed outbox message is {lag.TotalSeconds:F0}s old (threshold {threshold.TotalSeconds:F0}s).",
                data: data);
        }

        if (lag >= TimeSpan.FromTicks(threshold.Ticks / 2))
        {
            return HealthCheckResult.Degraded(
                $"Outbox lag {lag.TotalSeconds:F0}s approaching threshold {threshold.TotalSeconds:F0}s.",
                data: data);
        }

        return HealthCheckResult.Healthy("Outbox lag within acceptable bounds.", data);
    }
}
