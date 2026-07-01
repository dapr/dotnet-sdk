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

using Dapr.EntityFrameworkCore.Outbox;
using Dapr.EntityFrameworkCore.Outbox.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Samples.Outbox;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<OrdersDbContext>((sp, options) =>
{
    options.UseSqlite("Data Source=orders.db");
    options.AddInterceptors(sp.GetRequiredService<DaprOutboxSaveChangesInterceptor>());
});

builder.Services.AddDaprClient();
builder.Services.AddDaprOutbox<OrdersDbContext>(o =>
{
    o.PollInterval = TimeSpan.FromSeconds(2);
    o.BatchSize = 25;
    o.HealthCheckThreshold = TimeSpan.FromMinutes(1);
    o.RetentionPeriod = TimeSpan.FromDays(7);
})
    .AddDefaultDispatcher()
    .AddRetentionService()
    .AddOutboxHealthCheck();

var app = builder.Build();

// Create the SQLite file on first run.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    db.Database.EnsureCreated();
}

app.MapPost("/orders", async (CreateOrderRequest request, OrdersDbContext db, CancellationToken ct) =>
{
    var order = Order.Create(request.CustomerName, request.TotalAmount);
    db.Orders.Add(order);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/orders/{order.Id}", new { order.Id });
});

app.MapGet("/orders", async (OrdersDbContext db, CancellationToken ct) =>
    await db.Orders.AsNoTracking().ToListAsync(ct));

app.MapHealthChecks("/health");

app.Run();

public sealed record CreateOrderRequest(string CustomerName, decimal TotalAmount);
