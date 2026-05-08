// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

// This example demonstrates the Dapr State Management SDK. Run the Dapr sidecar first:
//
//   dapr run --app-id statemanagement-example --dapr-grpc-port 50001 -- dotnet run
//
// See IWidgetStore.cs for the source-generator usage pattern.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapr.StateManagement;
using Dapr.StateManagement.Extensions;
using Microsoft.Extensions.DependencyInjection;

// ── Dependency-injection setup ────────────────────────────────────────────────
//
// AddWidgetStore() is generated at compile time from [StateStore("statestore")] on
// IWidgetStore (see IWidgetStore.cs). It registers a WidgetStateStoreClient
// singleton that forwards all IDaprStateStoreClient calls with the store name
// pre-filled — no magic string "statestore" at call sites.

var services = new ServiceCollection();
services.AddDaprStateManagementClient()
    .AddWidgetStore();   // <-- 100% generated; produced from IWidgetStore : IDaprStateStoreClient

await using var provider = services.BuildServiceProvider();

// ── Pattern A: Inject the typed store client (recommended) ────────────────────
var store = provider.GetRequiredService<IWidgetStore>();
await RunWithTypedStoreAsync(store);

// ── Pattern B: Inject DaprStateManagementClient directly ─────────────────────
var client = provider.GetRequiredService<DaprStateManagementClient>();
await RunWithDirectClientAsync(client);

// ─────────────────────────────────────────────────────────────────────────────

static async Task RunWithTypedStoreAsync(IWidgetStore store)
{
    Console.WriteLine("=== Typed store (via source generator) ===");
    const string key = "my-widget";

    var widget = new Widget("medium", "blue");
    await store.SaveStateAsync(key, widget);
    Console.WriteLine($"Saved: key={key}");

    var loaded = await store.GetStateAsync<Widget>(key);
    Console.WriteLine($"Loaded: {loaded?.Size} / {loaded?.Color}");

    var (existingValue, etag) = await store.GetStateAndETagAsync<Widget>(key);
    if (etag is not null)
    {
        var updated = existingValue! with { Color = "green" };
        var saved = await store.TrySaveStateAsync(key, updated, etag);
        Console.WriteLine(saved ? "ETag save succeeded." : "ETag mismatch.");
    }

    await store.DeleteStateAsync(key);
    Console.WriteLine($"Deleted key={key}");
}

static async Task RunWithDirectClientAsync(DaprStateManagementClient client)
{
    Console.WriteLine("=== Direct DaprStateManagementClient ===");
    const string storeName = "statestore";

    await client.SaveBulkStateAsync(storeName, new List<SaveStateItem<Widget>>
    {
        new("widget-a", new Widget("small", "red")),
        new("widget-b", new Widget("large", "white")),
    });

    var bulk = await client.GetBulkStateAsync<Widget>(storeName, new[] { "widget-a", "widget-b" });
    foreach (var item in bulk)
        Console.WriteLine($"Bulk item: key={item.Key}, value={item.Value?.Size}/{item.Value?.Color}");

    await client.ExecuteStateTransactionAsync(storeName, new List<StateTransactionRequest>
    {
        new("widget-a", null, StateOperationType.Delete),
        new("widget-b", null, StateOperationType.Delete),
    });
    Console.WriteLine("Transaction committed.");
}

/// <summary>
/// A simple model demonstrating JSON-serializable state.
/// </summary>
/// <param name="Size">The widget size.</param>
/// <param name="Color">The widget color.</param>
internal sealed record Widget(string Size, string Color);
