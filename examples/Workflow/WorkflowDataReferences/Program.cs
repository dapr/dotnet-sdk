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

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkflowDataReferences;
using WorkflowDataReferences.Examples;

var configuration = Host.CreateApplicationBuilder(args).Configuration;
PayloadExample[] catalog =
[
    new InlinePayloadExample(),
    new AutomaticPayloadExample(),
    new AutomaticOutputChunksExample(),
    new ReferencePayloadExample(),
    new AutomaticReferencePayloadExample(),
    new StreamUploadPayloadExample(),
    new StreamUploadPayloadExample(automaticReferences: true),
    new ChunkStreamingPayloadExample()
];

// Listing and selecting scenarios happen before starting a host: --List=true needs no sidecar.
if (configuration.GetValue("List", false))
{
    foreach (var example in catalog)
        Console.WriteLine($"{example.Id,-22} {example.WhenToUse}");
    Console.WriteLine("Run with --Example=<name>, or --Example=all (the default).");
    return;
}

// Keep the original inline-only switch for users trying the sample without binary-store support.
var dataRefsEnabled = configuration.GetValue("DataRefs:Enabled", true);
var selection = configuration["Example"] ?? (dataRefsEnabled ? "all" : "inline");
var runAll = string.Equals(selection, "all", StringComparison.OrdinalIgnoreCase);
var examples = catalog.Where(example =>
    (runAll || string.Equals(example.Id, selection, StringComparison.OrdinalIgnoreCase)) &&
    (dataRefsEnabled || example.Id == "inline")).ToArray();
if (examples.Length == 0)
    throw new ArgumentException(
        $"Unknown or disabled example '{selection}'. Use --List=true for names; " +
        "DataRefs:Enabled=false allows only inline.");

var document = new DocumentPayload("sample.txt", new string('a', 128 * 1024));

Console.WriteLine("Workflow data references: choose storage, receiving types, and read APIs independently.");
Console.WriteLine("Object examples use a materialized 128 KiB document; stream examples generate a 32 MiB file.");
Console.WriteLine("Offloading reduces history size. Only streaming reads/writes avoid whole-payload buffers.");
Console.WriteLine("This is a behavior walkthrough, not a performance benchmark.");

for (var index = 0; index < examples.Length; index++)
{
    var example = examples[index];
    Console.WriteLine($"\n=== {index + 1}/{examples.Length}: {example.Id} - {example.Title} ===");
    Console.WriteLine($"Choose this when: {example.WhenToUse}");

    // Offloading is a host-wide setting, so the inline and offloaded examples need separate hosts.
    // Stop each worker before starting the next one against the same dedicated sidecar.
    // The source generator discovers the workflows/activities; each example configures only its policy.
    var builder = Host.CreateApplicationBuilder(args);
    example.ConfigureServices(builder.Services);
    using var host = builder.Build();
    await host.StartAsync();
    try
    {
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        await using var scope = host.Services.CreateAsyncScope();
        await example.RunAsync(scope.ServiceProvider, document, timeout.Token);
    }
    finally
    {
        using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await host.StopAsync(shutdown.Token);
    }
}

Console.WriteLine("\nHistories are retained for inspection using the printed workflow IDs.");
Console.WriteLine("Keep the demo sidecar running while referenced payloads are needed; its binary store is in-memory.");
