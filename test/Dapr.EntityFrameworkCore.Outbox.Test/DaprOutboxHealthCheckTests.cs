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
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Dapr.EntityFrameworkCore.Outbox.Test;

public class DaprOutboxHealthCheckTests
{
    [Fact]
    public async Task NoThreshold_ReturnsHealthy()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = SqliteOutboxHarness.Create();

        var hc = new DaprOutboxHealthCheck<TestDbContext>(
            harness.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new DaprOutboxOptions { HealthCheckThreshold = null }),
            TimeProvider.System);

        var result = await hc.CheckHealthAsync(new HealthCheckContext(), ct);
        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task NoUnprocessedMessages_ReturnsHealthy()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = SqliteOutboxHarness.Create();

        var hc = new DaprOutboxHealthCheck<TestDbContext>(
            harness.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new DaprOutboxOptions { HealthCheckThreshold = TimeSpan.FromSeconds(60) }),
            TimeProvider.System);

        var result = await hc.CheckHealthAsync(new HealthCheckContext(), ct);
        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task LagBelowHalfThreshold_ReturnsHealthy()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = SqliteOutboxHarness.Create();

        var now = DateTimeOffset.UtcNow;
        await Seed(harness, ct, now.AddSeconds(-10));

        var hc = new DaprOutboxHealthCheck<TestDbContext>(
            harness.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new DaprOutboxOptions { HealthCheckThreshold = TimeSpan.FromSeconds(60) }),
            new StubTimeProvider(now));

        var result = await hc.CheckHealthAsync(new HealthCheckContext(), ct);
        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task LagBetweenHalfAndFull_ReturnsDegraded()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = SqliteOutboxHarness.Create();

        var now = DateTimeOffset.UtcNow;
        await Seed(harness, ct, now.AddSeconds(-40));

        var hc = new DaprOutboxHealthCheck<TestDbContext>(
            harness.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new DaprOutboxOptions { HealthCheckThreshold = TimeSpan.FromSeconds(60) }),
            new StubTimeProvider(now));

        var result = await hc.CheckHealthAsync(new HealthCheckContext(), ct);
        result.Status.ShouldBe(HealthStatus.Degraded);
    }

    [Fact]
    public async Task LagAtOrAboveThreshold_ReturnsUnhealthy()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = SqliteOutboxHarness.Create();

        var now = DateTimeOffset.UtcNow;
        await Seed(harness, ct, now.AddSeconds(-120));

        var hc = new DaprOutboxHealthCheck<TestDbContext>(
            harness.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new DaprOutboxOptions { HealthCheckThreshold = TimeSpan.FromSeconds(60) }),
            new StubTimeProvider(now));

        var result = await hc.CheckHealthAsync(new HealthCheckContext(), ct);
        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    private static async Task Seed(SqliteOutboxHarness harness, CancellationToken ct, DateTimeOffset occurredAt)
    {
        using var scope = harness.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        db.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredAt = occurredAt,
            PubSubName = "pubsub",
            Topic = "topic",
            ContentType = "application/json",
            Payload = new byte[] { 1, 2, 3 },
        });
        await db.SaveChangesAsync(ct);
    }

    private sealed class StubTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset now;
        public StubTimeProvider(DateTimeOffset now) => this.now = now;
        public override DateTimeOffset GetUtcNow() => now;
    }
}
