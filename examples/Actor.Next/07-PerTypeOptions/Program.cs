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

using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.Options;
using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Examples.Store;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprActors(options =>
{
    // App-wide defaults for every actor type this app hosts.
    options.ActorIdleTimeout = TimeSpan.FromMinutes(30);
    options.DrainRebalancedActorsTimeout = TimeSpan.FromSeconds(10);

    // Checkout sessions are ephemeral: release idle instances quickly.
    options.Actors.RegisterActor<CheckoutSessionActor>(o =>
        o.IdleTimeout = TimeSpan.FromMinutes(5));

    // Inventory aggregates stay hot and allow reentrant call chains.
    options.Actors.RegisterActor<InventoryActor>(o =>
    {
        o.IdleTimeout = TimeSpan.FromHours(2);
        o.EnableReentrancy = true;
        o.MaxReentrantDepth = 4;
    });
});

var app = builder.Build();

app.MapGet("/", () => Results.Content(HomePage(), "text/html; charset=utf-8"));

app.MapPost("/sessions/{sessionId}/items", async (
    string sessionId,
    SessionItem item,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) =>
{
    await CreateSession(proxies, sessionId).AddItem(item, cancellationToken);
    return Results.NoContent();
});

app.MapGet("/sessions/{sessionId}/summary", (
    string sessionId,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) => CreateSession(proxies, sessionId).GetSummary(cancellationToken));

app.MapPost("/inventory/{sku}/adjust", (
    string sku,
    StockAdjustment adjustment,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) => CreateInventory(proxies, sku).Adjust(adjustment, cancellationToken));

app.MapGet("/inventory/{sku}", (
    string sku,
    IActorProxyFactory proxies,
    CancellationToken cancellationToken) => CreateInventory(proxies, sku).GetOnHand(cancellationToken));

await app.RunAsync();
return;

static ICheckoutSessionActor CreateSession(IActorProxyFactory proxies, string sessionId) =>
    proxies.Create<ICheckoutSessionActor>(ActorId.Create(sessionId), "CheckoutSession");

static IInventoryActor CreateInventory(IActorProxyFactory proxies, string sku) =>
    proxies.Create<IInventoryActor>(ActorId.Create(sku), "Inventory");

static string HomePage() => """
    <!doctype html>
    <html lang="en">
    <head>
      <meta charset="utf-8">
      <title>Per-type options sample</title>
      <style>
        body { font-family: system-ui, sans-serif; max-width: 48rem; margin: 2rem auto; padding: 0 1rem; line-height: 1.5; }
        table { border-collapse: collapse; margin: 1rem 0; }
        th, td { border: 1px solid #aaa; padding: .3rem .6rem; text-align: left; }
        code { background: #eef; padding: .1rem .3rem; border-radius: 3px; }
      </style>
    </head>
    <body>
      <h1>Per-type options sample</h1>
      <p>This app hosts two actor types with different lifecycle profiles, configured per type
         in <code>Program.cs</code> from a single <code>AddDaprActors</code> call. Only the fields
         set per type override the app-wide defaults; everything else is inherited. Each actor type
         is advertised to daprd with its merged configuration.</p>
      <table>
        <tr><th>Actor type</th><th>Idle timeout</th><th>Drain rebalanced</th><th>Reentrancy</th></tr>
        <tr><td>App-wide defaults</td><td>30 min</td><td>10 s</td><td>off</td></tr>
        <tr><td><code>CheckoutSession</code></td><td><strong>5 min</strong> (override)</td><td>10 s (inherited)</td><td>off (inherited)</td></tr>
        <tr><td><code>Inventory</code></td><td><strong>2 h</strong> (override)</td><td>10 s (inherited)</td><td><strong>on, max depth 4</strong> (override)</td></tr>
      </table>
      <h2>Endpoints</h2>
      <ul>
        <li><code>POST /sessions/{sessionId}/items</code> &mdash; add an item to a checkout session</li>
        <li><code>GET /sessions/{sessionId}/summary</code> &mdash; read the session summary</li>
        <li><code>POST /inventory/{sku}/adjust</code> &mdash; adjust stock on hand</li>
        <li><code>GET /inventory/{sku}</code> &mdash; read stock on hand</li>
      </ul>
      <p>Ready-made requests are in <code>Store.Next.Example07.http</code>.</p>
    </body>
    </html>
    """;
