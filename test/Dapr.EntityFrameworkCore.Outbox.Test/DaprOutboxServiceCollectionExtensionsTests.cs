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

using Dapr.EntityFrameworkCore.Outbox.DependencyInjection;
using Dapr.EntityFrameworkCore.Outbox.Test.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Dapr.EntityFrameworkCore.Outbox.Test;

public class DaprOutboxServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDaprOutbox_RegistersRequiredServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TestDbContext>(o => o.UseSqlite("DataSource=:memory:"));

        var builder = services.AddDaprOutbox<TestDbContext>();

        builder.ShouldNotBeNull();
        builder.DbContextType.ShouldBe(typeof(TestDbContext));
        builder.Services.ShouldBeSameAs(services);

        var sp = services.BuildServiceProvider();
        sp.GetService<IOutboxMessageFactory>().ShouldBeOfType<AttributeOutboxMessageFactory>();
        sp.GetService<IOutboxPendingBuffer>().ShouldBeOfType<OutboxPendingBuffer>();
        sp.GetService<TimeProvider>().ShouldBeSameAs(TimeProvider.System);

        using var scope = sp.CreateScope();
        scope.ServiceProvider.GetService<DaprOutboxSaveChangesInterceptor>().ShouldNotBeNull();
    }

    [Fact]
    public void AddDaprOutbox_ConfigureCallback_AppliesOptions()
    {
        var services = new ServiceCollection();
        services.AddDaprOutbox<TestDbContext>(opts =>
        {
            opts.TableName = "MyOutbox";
            opts.BatchSize = 200;
        });

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<DaprOutboxOptions>>().Value;

        options.TableName.ShouldBe("MyOutbox");
        options.BatchSize.ShouldBe(200);
    }

    [Fact]
    public void AddDaprOutbox_DefaultOptions_MatchLockedDefaults()
    {
        var services = new ServiceCollection();
        services.AddDaprOutbox<TestDbContext>();

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<DaprOutboxOptions>>().Value;

        options.TableName.ShouldBe(DaprOutboxOptions.DefaultTableName);
        options.TableName.ShouldBe("DaprOutboxMessages");
        options.BatchSize.ShouldBe(50);
        options.MaxAttempts.ShouldBe(10);
        options.PollInterval.ShouldBe(TimeSpan.FromSeconds(5));
        options.LockDuration.ShouldBe(TimeSpan.FromSeconds(30));
        options.ShutdownDrainTimeout.ShouldBe(TimeSpan.FromSeconds(30));
    }
}
