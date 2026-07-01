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

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Dapr.EntityFrameworkCore.Outbox.DependencyInjection;
using Dapr.EntityFrameworkCore.Outbox.Test.Fixtures;

namespace Dapr.EntityFrameworkCore.Outbox.Test.Fixtures;

/// <summary>
/// Boots an in-memory SQLite DbContext with the outbox interceptor and DI wired up.
/// Callers new one per test; disposal closes the shared SQLite connection.
/// </summary>
internal sealed class SqliteOutboxHarness : IAsyncDisposable
{
    public SqliteConnection Connection { get; }
    public ServiceProvider Services { get; }

    private SqliteOutboxHarness(SqliteConnection connection, ServiceProvider services)
    {
        Connection = connection;
        Services = services;
    }

    public static SqliteOutboxHarness Create(Action<DaprOutboxOptions>? configure = null)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TestDbContext>((sp, options) =>
        {
            options.UseSqlite(connection);
            options.AddInterceptors(sp.GetRequiredService<DaprOutboxSaveChangesInterceptor>());
        });
        services.AddDaprOutbox<TestDbContext>(configure);

        var sp = services.BuildServiceProvider();

        using (var scope = sp.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            ctx.Database.EnsureCreated();
        }

        return new SqliteOutboxHarness(connection, sp);
    }

    public IServiceScope NewScope() => Services.CreateScope();

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        await Connection.DisposeAsync();
    }
}
