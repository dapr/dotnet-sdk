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

using Dapr.EntityFrameworkCore.Outbox.Test.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Dapr.EntityFrameworkCore.Outbox.Test;

public sealed class OutboxRetentionHostedServiceTests
{
    [Fact]
    public async Task NoRetentionPeriod_ReturnsImmediately()
    {
        await using var harness = SqliteOutboxHarness.Create(o => o.RetentionPeriod = null);
        var opts = harness.Services.GetRequiredService<IOptions<DaprOutboxOptions>>();
        var scopeFactory = harness.Services.GetRequiredService<IServiceScopeFactory>();

        var svc = new OutboxRetentionHostedService<TestDbContext>(
            scopeFactory,
            opts,
            new StubTimeProvider(DateTimeOffset.UtcNow),
            NullLogger<OutboxRetentionHostedService<TestDbContext>>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await svc.StartAsync(cts.Token);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        await svc.StopAsync(cts.Token);
    }

    [Fact]
    public async Task DeletesOnlyProcessedRowsOlderThanRetention()
    {
        await using var harness = SqliteOutboxHarness.Create(o =>
        {
            o.UseCompactDateTimeStorage = true;
            o.RetentionPeriod = TimeSpan.FromHours(1);
        });

        var now = DateTimeOffset.UtcNow;
        using (var scope = harness.NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            db.Set<OutboxMessage>().AddRange(
                Row(now.AddHours(-2), processedAt: now.AddHours(-2)),  // old + processed -> DELETE
                Row(now.AddHours(-3), processedAt: now.AddMinutes(-5)), // recently processed -> KEEP
                Row(now.AddHours(-4), processedAt: null),               // pending -> KEEP
                Row(now.AddHours(-5), processedAt: null, attempts: 99)); // dead-lettered -> KEEP
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var svc = new OutboxRetentionHostedService<TestDbContext>(
            harness.Services.GetRequiredService<IServiceScopeFactory>(),
            harness.Services.GetRequiredService<IOptions<DaprOutboxOptions>>(),
            new StubTimeProvider(now),
            NullLogger<OutboxRetentionHostedService<TestDbContext>>.Instance);

        using var cts = new CancellationTokenSource();
        await svc.StartAsync(cts.Token);

        int remaining = 4;
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && remaining != 3)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
            using var scope = harness.NewScope();
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            remaining = await db.Set<OutboxMessage>().CountAsync(TestContext.Current.CancellationToken);
        }

        await svc.StopAsync(CancellationToken.None);
        remaining.ShouldBe(3);
    }

    private static OutboxMessage Row(DateTimeOffset occurredAt, DateTimeOffset? processedAt, int attempts = 0) =>
        new()
        {
            Id = Guid.NewGuid(),
            SchemaVersion = 1,
            OccurredAt = occurredAt,
            PubSubName = "pubsub",
            Topic = "widgets",
            ContentType = "application/json",
            Payload = new byte[] { 1 },
            ProcessedAt = processedAt,
            AttemptCount = attempts,
        };

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
