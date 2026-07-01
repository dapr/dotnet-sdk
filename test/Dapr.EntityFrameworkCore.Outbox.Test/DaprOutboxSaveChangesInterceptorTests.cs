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

public class DaprOutboxSaveChangesInterceptorTests
{
    [Fact]
    public async Task SaveChanges_WritesOutboxRow_FromDomainEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = SqliteOutboxHarness.Create();

        using (var scope = harness.NewScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var widget = new Widget { Id = 1, Name = "gizmo" };
            widget.Raise(new WidgetCreated { WidgetId = 1, Name = "gizmo" });
            ctx.Widgets.Add(widget);
            await ctx.SaveChangesAsync(ct);
        }

        using (var scope = harness.NewScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var rows = await ctx.Set<OutboxMessage>().ToListAsync(ct);

            rows.Count.ShouldBe(1);
            var row = rows[0];
            row.PubSubName.ShouldBe("pubsub");
            row.Topic.ShouldBe("widgets");
            row.ContentType.ShouldBe("application/json");
            row.Id.ShouldNotBe(Guid.Empty);
            row.OccurredAt.ShouldNotBe(default);
            row.ProcessedAt.ShouldBeNull();
            row.Payload.Length.ShouldBeGreaterThan(0);
        }
    }

    [Fact]
    public async Task SaveChanges_ClearsDomainEvents_OnAggregate()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = SqliteOutboxHarness.Create();

        Widget captured;
        using (var scope = harness.NewScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var widget = new Widget { Id = 2, Name = "clear" };
            widget.Raise(new WidgetCreated { WidgetId = 2, Name = "clear" });
            ctx.Widgets.Add(widget);
            await ctx.SaveChangesAsync(ct);
            captured = widget;
        }

        captured.DomainEvents.Count.ShouldBe(0);
    }

    [Fact]
    public async Task SaveChanges_WritesOutboxRow_FromExplicitEnqueue()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = SqliteOutboxHarness.Create();

        using (var scope = harness.NewScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            ctx.Widgets.Add(new Widget { Id = 3, Name = "explicit" });
            ctx.EnqueueOutbox(
                pubSubName: "pubsub",
                topic: "adhoc",
                payload: new { message = "hello" },
                correlationId: "corr-1");
            await ctx.SaveChangesAsync(ct);
        }

        using (var scope = harness.NewScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var row = await ctx.Set<OutboxMessage>().SingleAsync(ct);

            row.PubSubName.ShouldBe("pubsub");
            row.Topic.ShouldBe("adhoc");
            row.CorrelationId.ShouldBe("corr-1");
        }
    }

    [Fact]
    public async Task SaveChanges_RollingBackTransaction_DoesNotPersistOutboxRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = SqliteOutboxHarness.Create();

        using (var scope = harness.NewScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            await using var tx = await ctx.Database.BeginTransactionAsync(ct);

            var widget = new Widget { Id = 4, Name = "rolled-back" };
            widget.Raise(new WidgetCreated { WidgetId = 4, Name = "rolled-back" });
            ctx.Widgets.Add(widget);
            await ctx.SaveChangesAsync(ct);
            // deliberately not calling tx.CommitAsync — the using disposes and rolls back
        }

        using (var scope = harness.NewScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            (await ctx.Widgets.AnyAsync(ct)).ShouldBeFalse();
            (await ctx.Set<OutboxMessage>().AnyAsync(ct)).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task SaveChanges_WithNoEvents_WritesNoOutboxRows()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = SqliteOutboxHarness.Create();

        using (var scope = harness.NewScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            ctx.Widgets.Add(new Widget { Id = 5, Name = "silent" });
            await ctx.SaveChangesAsync(ct);
        }

        using (var scope = harness.NewScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            (await ctx.Set<OutboxMessage>().AnyAsync(ct)).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task SaveChanges_MixesDomainEventsAndExplicitEnqueues()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var harness = SqliteOutboxHarness.Create();

        using (var scope = harness.NewScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var widget = new Widget { Id = 6, Name = "mix" };
            widget.Raise(new WidgetCreated { WidgetId = 6, Name = "mix" });
            ctx.Widgets.Add(widget);
            ctx.EnqueueOutbox("pubsub", "adhoc", new { extra = 1 });
            await ctx.SaveChangesAsync(ct);
        }

        using (var scope = harness.NewScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            var rows = await ctx.Set<OutboxMessage>().OrderBy(o => o.Topic).ToListAsync(ct);
            rows.Count.ShouldBe(2);
            rows.Select(r => r.Topic).ShouldBe(new[] { "adhoc", "widgets" });
        }
    }
}
