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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dapr.EntityFrameworkCore.Outbox;

/// <summary>
/// Background service that periodically invokes <see cref="IOutboxDispatcher.DispatchPendingAsync"/>.
/// Honors <see cref="DaprOutboxOptions.PollInterval"/> and, on shutdown, waits up to
/// <see cref="DaprOutboxOptions.ShutdownDrainTimeout"/> for the in-flight iteration to finish.
/// </summary>
/// <typeparam name="TDbContext">The application <see cref="DbContext"/> the outbox belongs to.</typeparam>
public sealed class DaprOutboxHostedService<TDbContext> : BackgroundService
    where TDbContext : DbContext
{
    private static readonly Action<ILogger, string, Exception?> LogIterationError =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1, nameof(LogIterationError)),
            "Dapr outbox dispatch iteration failed for {DbContext}; will retry after PollInterval.");

    private static readonly Action<ILogger, string, Exception?> LogDrainStarted =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2, nameof(LogDrainStarted)),
            "Dapr outbox drain started for {DbContext}; waiting for in-flight iteration.");

    private static readonly Action<ILogger, string, Exception?> LogDrainTimeout =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(3, nameof(LogDrainTimeout)),
            "Dapr outbox drain for {DbContext} timed out; in-flight iteration will be cancelled.");

    private readonly IOutboxDispatcher dispatcher;
    private readonly IOptions<DaprOutboxOptions> options;
    private readonly ILogger<DaprOutboxHostedService<TDbContext>> logger;
    private Task? currentIteration;

    /// <summary>
    /// Creates a new hosted service.
    /// </summary>
    public DaprOutboxHostedService(
        IOutboxDispatcher dispatcher,
        IOptions<DaprOutboxOptions> options,
        ILogger<DaprOutboxHostedService<TDbContext>> logger)
    {
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var contextName = typeof(TDbContext).Name;
        var interval = options.Value.PollInterval;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                currentIteration = dispatcher.DispatchPendingAsync(stoppingToken);
                await currentIteration.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogIterationError(logger, contextName, ex);
            }
            finally
            {
                currentIteration = null;
            }

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        var contextName = typeof(TDbContext).Name;
        LogDrainStarted(logger, contextName, null);

        var drainTimeout = options.Value.ShutdownDrainTimeout;
        var inFlight = currentIteration;

        if (inFlight is null)
        {
            await base.StopAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var completed = await Task.WhenAny(inFlight, Task.Delay(drainTimeout, cancellationToken)).ConfigureAwait(false);
        if (completed != inFlight)
        {
            LogDrainTimeout(logger, contextName, null);
        }

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
