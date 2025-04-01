// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;

namespace Samples.Client;

public class StateStoreETagsExample : Example
{
    private static readonly string stateKeyName = "widget";
    private static readonly string storeName = "statestore";

    public override string DisplayName => "Using the State Store with ETags";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        using var client = new DaprClientBuilder().Build();

        // Save state which will create a new etag
        await client.SaveStateAsync<Widget>(storeName, stateKeyName,  new Widget() { Size = "small", Color = "yellow", }, cancellationToken: cancellationToken);
        Console.WriteLine($"Saved state which created a new entry with an initial etag");

        // Read the state and etag
        var (original, originalETag) = await client.GetStateAndETagAsync<Widget>(storeName, stateKeyName, cancellationToken: cancellationToken);
        Console.WriteLine($"Retrieved state: {original.Size}  {original.Color} with etag: {originalETag}");

        // Save state which will update the etag
        await client.SaveStateAsync<Widget>(storeName, stateKeyName,  new Widget() { Size = "small", Color = "yellow", }, cancellationToken: cancellationToken);
        Console.WriteLine($"Saved state with new etag");

        // Modify the state and try saving it with the old etag. This will fail
        original.Color = "purple";
        var isSaveStateSuccess = await client.TrySaveStateAsync<Widget>(storeName, stateKeyName, original, originalETag, cancellationToken: cancellationToken);
        Console.WriteLine($"Saved state with old etag: {originalETag} successfully? {isSaveStateSuccess}");

        // Read the updated state and etag
        var (updated, updatedETag) = await client.GetStateAndETagAsync<Widget>(storeName, stateKeyName, cancellationToken: cancellationToken);
        Console.WriteLine($"Retrieved state: {updated.Size}  {updated.Color} with etag: {updatedETag}");

        // Modify the state and try saving it with the updated etag. This will succeed
        updated.Color = "green";
        isSaveStateSuccess = await client.TrySaveStateAsync<Widget>(storeName, stateKeyName, updated, updatedETag, cancellationToken: cancellationToken);
        Console.WriteLine($"Saved state with latest etag: {updatedETag} successfully? {isSaveStateSuccess}");

        // Delete with an old etag. This will fail
        var isDeleteStateSuccess = await client.TryDeleteStateAsync(storeName, stateKeyName, originalETag, cancellationToken: cancellationToken);
        Console.WriteLine($"Deleted state with old etag: {originalETag} successfully? {isDeleteStateSuccess}");

        // Read the updated state and etag
        (updated, updatedETag) = await client.GetStateAndETagAsync<Widget>(storeName, stateKeyName, cancellationToken: cancellationToken);
        Console.WriteLine($"Retrieved state: {updated.Size}  {updated.Color} with etag: {updatedETag}");

        // Delete with updated etag. This will succeed
        isDeleteStateSuccess = await client.TryDeleteStateAsync(storeName, stateKeyName, updatedETag, cancellationToken: cancellationToken);
        Console.WriteLine($"Deleted state with updated etag: {updatedETag} successfully? {isDeleteStateSuccess}");
    }

    private class Widget
    {
        public string? Size { get; set; }
        public string? Color { get; set; }
    }
}