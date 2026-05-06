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

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Dapr.StateManagement;
using Dapr.StateManagement.Extensions;
using Microsoft.Extensions.DependencyInjection;

// ── Dependency-injection setup ────────────────────────────────────────────────

var services = new ServiceCollection();
services.AddDaprStateManagementClient();

await using var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<DaprStateManagementClient>();

await RunExamplesAsync(client);

static async Task RunExamplesAsync(DaprStateManagementClient client)
{
    const string storeName = "statestore";
    const string key = "my-widget";

    // ── Save a state entry ───────────────────────────────────────────────────
    var widget = new Widget("medium", "blue");
    await client.SaveStateAsync(storeName, key, widget);
    Console.WriteLine($"Saved state: key={key}");

    // ── Retrieve a state entry ───────────────────────────────────────────────
    var loaded = await client.GetStateAsync<Widget>(storeName, key);
    Console.WriteLine($"Loaded state: {loaded?.Size} / {loaded?.Color}");

    // ── Optimistic concurrency with ETag ─────────────────────────────────────
    var (existingValue, etag) = await client.GetStateAndETagAsync<Widget>(storeName, key);
    Console.WriteLine($"ETag: {etag}");

    if (etag is not null)
    {
        var updated = existingValue! with { Color = "green" };
        var saved = await client.TrySaveStateAsync(storeName, key, updated, etag);
        Console.WriteLine(saved ? "ETag save succeeded." : "ETag mismatch — value was concurrently modified.");
    }

    // ── Bulk save and get ─────────────────────────────────────────────────────
    await client.SaveBulkStateAsync(storeName, new List<SaveStateItem<Widget>>
    {
        new("widget-a", new Widget("small", "red")),
        new("widget-b", new Widget("large", "white")),
    });

    var bulk = await client.GetBulkStateAsync<Widget>(storeName, new[] { "widget-a", "widget-b" });
    foreach (var item in bulk)
    {
        Console.WriteLine($"Bulk item: key={item.Key}, value={item.Value?.Size}/{item.Value?.Color}");
    }

    // ── Transactional write ───────────────────────────────────────────────────
    await client.ExecuteStateTransactionAsync(storeName, new List<StateTransactionRequest>
    {
        new("widget-a",
            JsonSerializer.SerializeToUtf8Bytes(new Widget("small", "purple")),
            StateOperationType.Upsert),
        new("widget-b", null, StateOperationType.Delete),
    });
    Console.WriteLine("Transaction committed.");

    // ── Delete ────────────────────────────────────────────────────────────────
    await client.DeleteStateAsync(storeName, key);
    Console.WriteLine($"Deleted key={key}");

    // ── Verify deletion ────────────────────────────────────────────────────────
    var afterDelete = await client.GetStateAsync<Widget>(storeName, key);
    Console.WriteLine($"After delete: {(afterDelete is null ? "null (expected)" : afterDelete.ToString())}");
}

/// <summary>
/// A simple model demonstrating JSON-serializable state.
/// </summary>
/// <param name="Size">The widget size.</param>
/// <param name="Color">The widget color.</param>
internal sealed record Widget(string Size, string Color);
