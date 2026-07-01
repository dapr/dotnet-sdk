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

using Microsoft.EntityFrameworkCore;

namespace Dapr.EntityFrameworkCore.Outbox.Test.Fixtures;

public sealed class Widget : IHasDomainEvents
{
    private readonly List<object> events = new();

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public IReadOnlyCollection<object> DomainEvents => events;

    public void Raise(object @event) => events.Add(@event);
    public void ClearDomainEvents() => events.Clear();
}

[DaprOutboxEvent("pubsub", "widgets")]
public sealed class WidgetCreated
{
    public int WidgetId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class UnroutedEvent
{
    public string Payload { get; set; } = string.Empty;
}

public sealed class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

    public DbSet<Widget> Widgets => Set<Widget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Widget>(entity =>
        {
            entity.ToTable("Widgets");
            entity.HasKey(w => w.Id);
            entity.Ignore(w => w.DomainEvents);
        });

        modelBuilder.AddDaprOutbox(o => o.UseCompactDateTimeStorage = true);
    }
}
