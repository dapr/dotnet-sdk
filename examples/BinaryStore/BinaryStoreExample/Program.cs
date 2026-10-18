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

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapr.BinaryStore;
using Dapr.BinaryStore.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddDaprBinaryStoreClient()
    .WithDocumentStore();

await using var provider = services.BuildServiceProvider();

await RunWithTypedStoreAsync(provider.GetRequiredService<IDocumentStore>());
await RunWithDirectClientAsync(provider.GetRequiredService<DaprBinaryStoreClient>());

static async Task RunWithTypedStoreAsync(IDocumentStore store)
{
    Console.WriteLine("=== Typed store (via source generator) ===");
    const string fileName = "documents/greeting.txt";

    var content = Encoding.UTF8.GetBytes("Hello from the Dapr binary store!");
    await store.SetBinaryFileAsync(fileName, content.AsMemory());
    var loaded = await store.GetBinaryFileBytesAsync(fileName);
    Console.WriteLine($"Downloaded: {Encoding.UTF8.GetString(loaded)}");

    var replacement = Encoding.UTF8.GetBytes("Updated document.");
    await store.SetBinaryFileAsync(fileName, replacement.AsMemory(), overwrite: true);
    var updated = await store.GetBinaryFileBytesAsync(fileName);
    if (!replacement.SequenceEqual(updated))
        throw new InvalidOperationException("The downloaded document does not match the replacement.");
    Console.WriteLine($"Overwritten: {Encoding.UTF8.GetString(updated)}");

    await store.DeleteBinaryFileAsync(fileName);
    Console.WriteLine($"Deleted: {fileName}");
}

static async Task RunWithDirectClientAsync(DaprBinaryStoreClient client)
{
    Console.WriteLine("=== Direct DaprBinaryStoreClient ===");
    const string storeName = "binarystore";
    const string fileName = "files/payload.bin";

    var content = new byte[96 * 1024 + 17];
    new Random(42).NextBytes(content);
    using var source = new MemoryStream(content, writable: false);
    await client.SetBinaryFileAsync(storeName, fileName, source, chunkSize: 16 * 1024);
    Console.WriteLine($"Uploaded: {content.Length} bytes in 16 KiB chunks");

    using var destination = new MemoryStream();
    await client.GetBinaryFileAsync(storeName, fileName, destination);
    if (!content.SequenceEqual(destination.ToArray()))
        throw new InvalidOperationException("The downloaded file does not match the uploaded content.");
    Console.WriteLine($"Downloaded and verified: {destination.Length} bytes");

    long totalBytes = 0;
    await foreach (var chunk in client.GetBinaryFileChunksAsync(storeName, fileName))
        totalBytes += chunk.Length;
    Console.WriteLine($"Read as an asynchronous sequence of chunks: {totalBytes} bytes");

    await client.DeleteBinaryFileAsync(storeName, fileName);
    Console.WriteLine($"Deleted: {fileName}");
}
