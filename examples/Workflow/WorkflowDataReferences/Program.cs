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
var dataRefsEnabled = configuration.GetValue("DataRefs:Enabled", true);
PayloadExample[] examples = dataRefsEnabled
    ? [new InlinePayloadExample(), new AutomaticPayloadExample(), new ReferencePayloadExample()]
    : [new InlinePayloadExample()];
var document = new DocumentPayload("sample.txt", new string('a', 128 * 1024));

Console.WriteLine("Workflow payload approaches");
Console.WriteLine($"Each approach starts with the same document: {document.Name}, {document.Content.Length:N0} characters.");
Console.WriteLine("Inline and automatic uppercase the document; explicit references stream and measure its serialized bytes.");
Console.WriteLine("This is a behavior comparison, not a performance benchmark.");
if (!dataRefsEnabled)
    Console.WriteLine("DataRefs:Enabled=false: running inline only; no payload-storage services will be registered.");

for (var index = 0; index < examples.Length; index++)
{
    var example = examples[index];
    Console.WriteLine($"\n=== {index + 1}/{examples.Length}: {example.Title} ===");

    // Offloading is a host-wide setting, so the inline and offloaded examples need separate hosts.
    // Stop each worker before starting the next one against the same dedicated sidecar.
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
