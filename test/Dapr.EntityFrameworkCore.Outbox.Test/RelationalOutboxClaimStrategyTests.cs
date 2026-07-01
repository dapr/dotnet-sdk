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
using Shouldly;
using Xunit;

namespace Dapr.EntityFrameworkCore.Outbox.Test;

public class RelationalOutboxClaimStrategyTests
{
    [Fact]
    public async Task ClaimBatch_ReturnsUnlockedRowsOrderedByOccurredAt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = SqliteOutboxHarness.Create(opts => opts.BatchSize = 10);
        var now = DateTimeOffset.UtcNow;
        await SeedAsync(harness, ct,
            NewRow("A", now.AddSeconds(-10)),
            NewRow("B", now.AddSeconds(-30)),
            NewRow("C", now.AddSeconds(-20)));

        var strategy = new RelationalOutboxClaimStrategy();
        using var scope = harness.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        var claimed = await strategy.ClaimBatchAsync(db, new DaprOutboxOptions(), "owner-1", now, ct);

        claimed.Count.ShouldBe(3);
        claimed.Select(m => m.Topic).ShouldBe(new[] { "B", "C", "A" });
        claimed.ShouldAllBe(m => m.LockOwner == "owner-1");
        claimed.ShouldAllBe(m => m.AttemptCount == 1);
    }

    [Fact]
    public async Task ClaimBatch_SkipsRowsWithLiveLock()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = SqliteOutboxHarness.Create();
        var now = DateTimeOffset.UtcNow;

        var locked = NewRow("locked", now.AddSeconds(-60));
        locked.LockOwner = "someone-else";
        locked.LockedUntil = now.AddSeconds(30);

        var free = NewRow("free", now.AddSeconds(-10));

        await SeedAsync(harness, ct, locked, free);

        var strategy = new RelationalOutboxClaimStrategy();
        using var scope = harness.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        var claimed = await strategy.ClaimBatchAsync(db, new DaprOutboxOptions(), "me", now, ct);

        claimed.Count.ShouldBe(1);
        claimed[0].Topic.ShouldBe("free");
    }

    [Fact]
    public async Task ClaimBatch_SkipsDeadLetteredRows()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = SqliteOutboxHarness.Create(opts => opts.MaxAttempts = 3);
        var now = DateTimeOffset.UtcNow;

        var dead = NewRow("dead", now.AddSeconds(-60));
        dead.AttemptCount = 3;

        var live = NewRow("live", now.AddSeconds(-10));

        await SeedAsync(harness, ct, dead, live);

        var strategy = new RelationalOutboxClaimStrategy();
        using var scope = harness.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        var opts = new DaprOutboxOptions { MaxAttempts = 3 };
        var claimed = await strategy.ClaimBatchAsync(db, opts, "me", now, ct);

        claimed.Count.ShouldBe(1);
        claimed[0].Topic.ShouldBe("live");
    }

    [Fact]
    public async Task Release_MarksSuccessesAndFailuresAndPersists()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = SqliteOutboxHarness.Create();
        var now = DateTimeOffset.UtcNow;
        var okRow = NewRow("ok", now.AddSeconds(-10));
        var badRow = NewRow("bad", now.AddSeconds(-5));

        await SeedAsync(harness, ct, okRow, badRow);

        var strategy = new RelationalOutboxClaimStrategy();
        using (var scope = harness.NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            await strategy.ClaimBatchAsync(db, new DaprOutboxOptions(), "owner", now, ct);
        }

        var future = now.AddMinutes(1);
        using (var scope = harness.NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var results = new[]
            {
                new OutboxDispatchResult(okRow.Id, true, null, null, 1),
                new OutboxDispatchResult(badRow.Id, false, "boom", future, 2),
            };
            await strategy.ReleaseAsync(db, results, "owner", ct);
        }

        using (var scope = harness.NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var rows = await db.Set<OutboxMessage>().OrderBy(r => r.Topic).ToListAsync(ct);
            var bad = rows[0];
            var ok = rows[1];

            ok.ProcessedAt.ShouldNotBeNull();
            ok.LockOwner.ShouldBeNull();
            ok.LockedUntil.ShouldBeNull();

            bad.ProcessedAt.ShouldBeNull();
            bad.LastError.ShouldBe("boom");
            bad.AttemptCount.ShouldBe(2);
            bad.LockedUntil.ShouldNotBeNull();
        }
    }

    private static OutboxMessage NewRow(string topic, DateTimeOffset occurredAt)
        => new()
        {
            Id = Guid.NewGuid(),
            SchemaVersion = 1,
            OccurredAt = occurredAt,
            PubSubName = "pubsub",
            Topic = topic,
            ContentType = "application/json",
            Payload = System.Text.Encoding.UTF8.GetBytes("{}"),
        };

    private static async Task SeedAsync(SqliteOutboxHarness harness, CancellationToken ct, params OutboxMessage[] rows)
    {
        using var scope = harness.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        db.Set<OutboxMessage>().AddRange(rows);
        await db.SaveChangesAsync(ct);
    }
}
