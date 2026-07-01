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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dapr.EntityFrameworkCore.Outbox;

/// <summary>
/// Default polling dispatcher. Each iteration opens a scope, claims a batch via
/// <see cref="IOutboxClaimStrategy"/>, publishes each message via
/// <see cref="DaprClient.PublishByteEventAsync"/> emitting <c>cloudevent.id</c> equal to the
/// row's <c>Id</c> for stable consumer-side idempotency, then persists per-message outcomes.
/// </summary>
/// <typeparam name="TDbContext">The application's <see cref="DbContext"/> containing the outbox table.</typeparam>
public sealed class PollingOutboxDispatcher<TDbContext> : IOutboxDispatcher
    where TDbContext : DbContext
{
    private static readonly Action<ILogger, int, string, Exception?> LogBatchClaimed =
        LoggerMessage.Define<int, string>(
            LogLevel.Debug,
            new EventId(1, nameof(LogBatchClaimed)),
            "Dapr outbox claimed batch of {Count} message(s) as {LockOwner}.");

    private static readonly Action<ILogger, Guid, string, string, Exception?> LogPublished =
        LoggerMessage.Define<Guid, string, string>(
            LogLevel.Debug,
            new EventId(2, nameof(LogPublished)),
            "Dapr outbox published message {MessageId} to {PubSubName}/{Topic}.");

    private static readonly Action<ILogger, Guid, string, string, int, Exception?> LogPublishFailed =
        LoggerMessage.Define<Guid, string, string, int>(
            LogLevel.Warning,
            new EventId(3, nameof(LogPublishFailed)),
            "Dapr outbox publish failed for message {MessageId} to {PubSubName}/{Topic}; attempt {AttemptCount}.");

    private static readonly Action<ILogger, Guid, int, Exception?> LogDeadLettered =
        LoggerMessage.Define<Guid, int>(
            LogLevel.Error,
            new EventId(4, nameof(LogDeadLettered)),
            "Dapr outbox message {MessageId} exceeded MaxAttempts ({MaxAttempts}); dead-lettered.");

    private readonly IServiceScopeFactory scopeFactory;
    private readonly DaprClient daprClient;
    private readonly IOptions<DaprOutboxOptions> options;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<PollingOutboxDispatcher<TDbContext>> logger;
    private readonly string dispatcherId;

    /// <summary>
    /// Creates a new polling dispatcher.
    /// </summary>
    public PollingOutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        DaprClient daprClient,
        IOptions<DaprOutboxOptions> options,
        TimeProvider timeProvider,
        ILogger<PollingOutboxDispatcher<TDbContext>> logger)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.daprClient = daprClient ?? throw new ArgumentNullException(nameof(daprClient));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.dispatcherId = $"{Environment.MachineName}:{Guid.NewGuid():N}";
    }

    /// <inheritdoc />
    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var claimStrategy = scope.ServiceProvider.GetRequiredService<IOutboxClaimStrategy>();
        var opts = options.Value;

        using var activity = DaprOutboxDiagnostics.ActivitySource.StartActivity(
            "outbox.dispatch", ActivityKind.Internal);

        var now = timeProvider.GetUtcNow();

        IReadOnlyList<OutboxMessage> batch;
        try
        {
            batch = await claimStrategy.ClaimBatchAsync(db, opts, dispatcherId, now, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }

        if (batch.Count == 0)
        {
            return 0;
        }

        LogBatchClaimed(logger, batch.Count, dispatcherId, null);
        activity?.SetTag("outbox.batch_size", batch.Count);

        var results = new List<OutboxDispatchResult>(batch.Count);
        var backoffCap = TimeSpan.FromMinutes(5);

        foreach (var message in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var metadata = BuildMetadata(message, opts);
                await daprClient.PublishByteEventAsync(
                    pubsubName: message.PubSubName,
                    topicName: message.Topic,
                    data: message.Payload,
                    dataContentType: message.ContentType,
                    metadata: metadata,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                results.Add(new OutboxDispatchResult(
                    MessageId: message.Id,
                    Succeeded: true,
                    Error: null,
                    NextLockedUntil: null,
                    AttemptCount: message.AttemptCount));

                LogPublished(logger, message.Id, message.PubSubName, message.Topic, null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var backoff = ComputeBackoff(message.AttemptCount, backoffCap);
                var nextLockedUntil = message.AttemptCount >= opts.MaxAttempts
                    ? (DateTimeOffset?)null
                    : timeProvider.GetUtcNow().Add(backoff);

                results.Add(new OutboxDispatchResult(
                    MessageId: message.Id,
                    Succeeded: false,
                    Error: ex.Message,
                    NextLockedUntil: nextLockedUntil,
                    AttemptCount: message.AttemptCount));

                LogPublishFailed(logger, message.Id, message.PubSubName, message.Topic, message.AttemptCount, ex);

                if (message.AttemptCount >= opts.MaxAttempts)
                {
                    LogDeadLettered(logger, message.Id, opts.MaxAttempts, ex);
                }
            }
        }

        await claimStrategy.ReleaseAsync(db, results, dispatcherId, cancellationToken).ConfigureAwait(false);
        return batch.Count;
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "MetadataJson is a Dictionary<string,string> serialized with the built-in JsonTypeInfo. When JsonTypeInfoResolver is set it takes precedence.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "MetadataJson deserialization falls through to reflection only when no JsonTypeInfoResolver is configured; documented AOT path uses the resolver.")]
    private static Dictionary<string, string> BuildMetadata(OutboxMessage message, DaprOutboxOptions opts)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(message.MetadataJson))
        {
            Dictionary<string, string>? existing;
            if (opts.JsonTypeInfoResolver is IJsonTypeInfoResolver resolver)
            {
                var jsonOptions = new JsonSerializerOptions(opts.JsonSerializerOptions ?? JsonSerializerOptions.Default)
                {
                    TypeInfoResolver = resolver,
                };
                existing = JsonSerializer.Deserialize<Dictionary<string, string>>(message.MetadataJson, jsonOptions);
            }
            else
            {
                existing = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    message.MetadataJson, opts.JsonSerializerOptions);
            }

            if (existing is not null)
            {
                foreach (var kv in existing)
                {
                    metadata[kv.Key] = kv.Value;
                }
            }
        }

        // Always emit cloudevent.id = OutboxMessage.Id for deterministic consumer-side dedup.
        metadata[DaprOutboxMetadata.CloudEventId] = message.Id.ToString();

        if (!string.IsNullOrEmpty(message.CorrelationId))
        {
            metadata["traceparent"] = message.CorrelationId!;
        }

        return metadata;
    }

    private static TimeSpan ComputeBackoff(int attempt, TimeSpan cap)
    {
        if (attempt <= 0)
        {
            return TimeSpan.FromSeconds(1);
        }

        var seconds = Math.Min(cap.TotalSeconds, Math.Pow(2, attempt));
        return TimeSpan.FromSeconds(seconds);
    }
}
