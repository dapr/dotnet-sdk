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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dapr.EntityFrameworkCore.Outbox;

/// <summary>
/// Background service that periodically deletes successfully processed outbox rows
/// older than <see cref="DaprOutboxOptions.RetentionPeriod"/>. Runs every hour by default,
/// or at whatever cadence the <see cref="DaprOutboxOptions.RetentionPeriod"/> allows.
/// Only rows with <c>ProcessedAt IS NOT NULL</c> and <c>ProcessedAt &lt; now - RetentionPeriod</c>
/// are deleted; dead-lettered and pending rows are preserved for operator inspection.
/// </summary>
/// <typeparam name="TDbContext">The application <see cref="DbContext"/> containing the outbox.</typeparam>
public sealed class OutboxRetentionHostedService<TDbContext> : BackgroundService
    where TDbContext : DbContext
{
    private static readonly Action<ILogger, int, string, Exception?> LogRetentionApplied =
        LoggerMessage.Define<int, string>(
            LogLevel.Information,
            new EventId(1, nameof(LogRetentionApplied)),
            "Dapr outbox retention deleted {Count} processed row(s) from {DbContext}.");

    private static readonly Action<ILogger, string, Exception?> LogRetentionFailed =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(2, nameof(LogRetentionFailed)),
            "Dapr outbox retention iteration failed for {DbContext}; will retry next cycle.");

    private readonly IServiceScopeFactory scopeFactory;
    private readonly IOptions<DaprOutboxOptions> options;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<OutboxRetentionHostedService<TDbContext>> logger;

    /// <summary>
    /// Creates the retention hosted service.
    /// </summary>
    public OutboxRetentionHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<DaprOutboxOptions> options,
        TimeProvider timeProvider,
        ILogger<OutboxRetentionHostedService<TDbContext>> logger)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        if (opts.RetentionPeriod is not TimeSpan retention)
        {
            // Nothing to do; exit gracefully so hosting doesn't hold a background loop.
            return;
        }

        // Sweep on a cadence proportional to the retention window so we don't scan too often
        // for long retention (e.g., 30 days) and not too rarely for short retention (e.g., 5 min).
        var cadence = TimeSpan.FromTicks(Math.Max(retention.Ticks / 24, TimeSpan.FromMinutes(1).Ticks));
        var contextName = typeof(TDbContext).Name;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
                var cutoff = timeProvider.GetUtcNow() - retention;

                var deleted = await db.Set<OutboxMessage>()
                    .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
                    .ExecuteDeleteAsync(stoppingToken).ConfigureAwait(false);

                if (deleted > 0)
                {
                    LogRetentionApplied(logger, deleted, contextName, null);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogRetentionFailed(logger, contextName, ex);
            }

            try
            {
                await Task.Delay(cadence, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
