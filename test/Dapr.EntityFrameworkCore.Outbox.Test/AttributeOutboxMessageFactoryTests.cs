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
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Dapr.EntityFrameworkCore.Outbox.Test;

public class AttributeOutboxMessageFactoryTests
{
    private static AttributeOutboxMessageFactory NewFactory(DaprOutboxOptions? options = null)
        => new(Options.Create(options ?? new DaprOutboxOptions()));

    [Fact]
    public void CreateFromDomainEvent_UsesAttributeRouting()
    {
        var factory = NewFactory();
        using var ctx = NewContext();

        var message = factory.CreateFromDomainEvent(new WidgetCreated { WidgetId = 1, Name = "a" }, ctx);

        message.PubSubName.ShouldBe("pubsub");
        message.Topic.ShouldBe("widgets");
        message.ContentType.ShouldBe("application/json");
        message.MetadataJson.ShouldNotBeNullOrEmpty();
        message.MetadataJson!.ShouldContain("cloudevent.type");
        message.MetadataJson!.ShouldContain("WidgetCreated");
    }

    [Fact]
    public void CreateFromDomainEvent_WithoutAttribute_Throws()
    {
        var factory = NewFactory();
        using var ctx = NewContext();

        Should.Throw<InvalidOperationException>(
            () => factory.CreateFromDomainEvent(new UnroutedEvent { Payload = "x" }, ctx));
    }

    [Fact]
    public void CreateFromExplicit_UsesProvidedRouting()
    {
        var factory = NewFactory();
        using var ctx = NewContext();

        var message = factory.CreateFromExplicit(
            "explicit-pubsub", "explicit-topic",
            new { Value = 42 },
            metadata: new Dictionary<string, string> { ["ttlInSeconds"] = "60" },
            correlationId: "abc",
            ctx);

        message.PubSubName.ShouldBe("explicit-pubsub");
        message.Topic.ShouldBe("explicit-topic");
        message.CorrelationId.ShouldBe("abc");
        message.MetadataJson!.ShouldContain("ttlInSeconds");
    }

    [Fact]
    public void CreateFromExplicit_ValidatesInputs()
    {
        var factory = NewFactory();
        using var ctx = NewContext();

        Should.Throw<ArgumentException>(() => factory.CreateFromExplicit("", "t", new { }, null, null, ctx));
        Should.Throw<ArgumentException>(() => factory.CreateFromExplicit("p", "", new { }, null, null, ctx));
        Should.Throw<ArgumentNullException>(() => factory.CreateFromExplicit("p", "t", null!, null, null, ctx));
    }

    private static TestDbContext NewContext()
    {
        var opts = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        return new TestDbContext(opts);
    }
}
