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

using Dapr.Client;
using Dapr.EntityFrameworkCore.Outbox.Test.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace Dapr.EntityFrameworkCore.Outbox.Test;

public class PollingOutboxDispatcherTests
{
    [Fact]
    public async Task Dispatch_PublishesEachMessage_WithCloudEventId()
    {
        var ct = TestContext.Current.CancellationToken;
        var messages = new[]
        {
            NewMessage("orders", "created", "corr-1"),
            NewMessage("orders", "updated"),
        };

        var strategy = new FakeClaimStrategy(messages);
        var daprClient = new Mock<DaprClient>();
        daprClient
            .Setup(c => c.PublishByteEventAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var (dispatcher, sp) = BuildDispatcher(strategy, daprClient.Object);
        await using (sp)
        {
            var processed = await dispatcher.DispatchPendingAsync(ct);

            processed.ShouldBe(2);
            daprClient.Verify(c => c.PublishByteEventAsync(
                "orders",
                It.IsAny<string>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                "application/json",
                It.Is<Dictionary<string, string>>(m =>
                    m.ContainsKey(DaprOutboxMetadata.CloudEventId)),
                It.IsAny<CancellationToken>()), Times.Exactly(2));

            strategy.Released.Count.ShouldBe(2);
            strategy.Released.ShouldAllBe(r => r.Succeeded);
            // Correlation ID becomes traceparent.
            var recorded = daprClient.Invocations
                .Where(i => i.Method.Name == nameof(DaprClient.PublishByteEventAsync))
                .Select(i => (Dictionary<string, string>?)i.Arguments[4])
                .ToList();
            recorded.Any(m => m is not null && m.ContainsKey("traceparent") && m["traceparent"] == "corr-1")
                .ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Dispatch_OnPublishFailure_RecordsFailureWithBackoff()
    {
        var ct = TestContext.Current.CancellationToken;
        var msg = NewMessage("orders", "created");
        var strategy = new FakeClaimStrategy(new[] { msg });
        var daprClient = new Mock<DaprClient>();
        daprClient
            .Setup(c => c.PublishByteEventAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sidecar down"));

        var (dispatcher, sp) = BuildDispatcher(strategy, daprClient.Object);
        await using (sp)
        {
            await dispatcher.DispatchPendingAsync(ct);

            strategy.Released.Count.ShouldBe(1);
            var result = strategy.Released[0];
            result.Succeeded.ShouldBeFalse();
            result.Error.ShouldBe("sidecar down");
            result.NextLockedUntil.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task Dispatch_WhenMaxAttemptsReached_LeavesRowInDeadLetterState()
    {
        var ct = TestContext.Current.CancellationToken;
        var msg = NewMessage("orders", "created", attemptCount: 10);
        var strategy = new FakeClaimStrategy(new[] { msg });
        var daprClient = new Mock<DaprClient>();
        daprClient
            .Setup(c => c.PublishByteEventAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("still failing"));

        var (dispatcher, sp) = BuildDispatcher(strategy, daprClient.Object, opts => opts.MaxAttempts = 10);
        await using (sp)
        {
            await dispatcher.DispatchPendingAsync(ct);

            var result = strategy.Released.Single();
            result.Succeeded.ShouldBeFalse();
            result.NextLockedUntil.ShouldBeNull();
        }
    }

    [Fact]
    public async Task Dispatch_EmptyBatch_MakesNoDaprCalls()
    {
        var ct = TestContext.Current.CancellationToken;
        var strategy = new FakeClaimStrategy(Array.Empty<OutboxMessage>());
        var daprClient = new Mock<DaprClient>(MockBehavior.Strict);

        var (dispatcher, sp) = BuildDispatcher(strategy, daprClient.Object);
        await using (sp)
        {
            var processed = await dispatcher.DispatchPendingAsync(ct);
            processed.ShouldBe(0);
            strategy.Released.Count.ShouldBe(0);
        }
    }

    private static OutboxMessage NewMessage(string pubsub, string topic, string? correlationId = null, int attemptCount = 0)
        => new()
        {
            Id = Guid.NewGuid(),
            SchemaVersion = 1,
            OccurredAt = DateTimeOffset.UtcNow,
            PubSubName = pubsub,
            Topic = topic,
            ContentType = "application/json",
            Payload = System.Text.Encoding.UTF8.GetBytes("{\"ok\":true}"),
            CorrelationId = correlationId,
            AttemptCount = attemptCount,
        };

    private static (PollingOutboxDispatcher<TestDbContext> Dispatcher, ServiceProvider Services) BuildDispatcher(
        IOutboxClaimStrategy strategy, DaprClient daprClient, Action<DaprOutboxOptions>? configureOptions = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TestDbContext>(o => o.UseSqlite("DataSource=:memory:"));
        services.AddScoped(_ => strategy);
        var options = new DaprOutboxOptions();
        configureOptions?.Invoke(options);
        var sp = services.BuildServiceProvider();

        var dispatcher = new PollingOutboxDispatcher<TestDbContext>(
            sp.GetRequiredService<IServiceScopeFactory>(),
            daprClient,
            Options.Create(options),
            TimeProvider.System,
            NullLogger<PollingOutboxDispatcher<TestDbContext>>.Instance);
        return (dispatcher, sp);
    }

    private sealed class FakeClaimStrategy : IOutboxClaimStrategy
    {
        private readonly IReadOnlyList<OutboxMessage> batch;
        public List<OutboxDispatchResult> Released { get; } = new();

        public FakeClaimStrategy(IReadOnlyList<OutboxMessage> batch) => this.batch = batch;

        public Task<IReadOnlyList<OutboxMessage>> ClaimBatchAsync(
            DbContext dbContext, DaprOutboxOptions options, string lockOwner, DateTimeOffset now, CancellationToken cancellationToken)
            => Task.FromResult(batch);

        public Task ReleaseAsync(
            DbContext dbContext, IReadOnlyList<OutboxDispatchResult> results, string lockOwner, CancellationToken cancellationToken)
        {
            Released.AddRange(results);
            return Task.CompletedTask;
        }
    }
}
