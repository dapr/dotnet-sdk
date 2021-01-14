// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;

namespace Samples.Client
{
    public class StateStoreETagsExample : Example
    {
        private static readonly string stateKeyName = "widget";
        private static readonly string storeName = "statestore";

        public override string DisplayName => "Using the State Store with ETags";

        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            var client = new DaprClientBuilder().Build();

            // Save state which will create an etag for the state entry
            await client.SaveStateAsync(storeName, stateKeyName, new Widget() { Size = "small", Color = "yellow", });

            // Read the state and etag
            var (original, originalETag) = await client.GetStateAndETagAsync<Widget>(storeName, stateKeyName);
            Console.WriteLine($"Retrieved state: {original.Size}  {original.Color} with etag: {originalETag}");

            // Modify the state which will update the etag
            original.Color = "orange";
            await client.SaveStateAsync<Widget>(storeName, stateKeyName, original);
            Console.WriteLine($"Saved modified state : {original.Size}  {original.Color}");

            // Read the updated state and etag
            var (updated, updatedETag) = await client.GetStateAndETagAsync<Widget>(storeName, stateKeyName);
            Console.WriteLine($"Retrieved state: {updated.Size}  {updated.Color} with etag: {updatedETag}");

            // Modify the state and try saving it with the old etag. This will fail
            updated.Color = "purple";
            var isSaveStateSuccess = await client.TrySaveStateAsync<Widget>(storeName, stateKeyName, updated, originalETag);
            Console.WriteLine($"Saved state with old etag: {originalETag} successfully? {isSaveStateSuccess}");

            // Modify the state and try saving it with the updated etag. This will succeed
            isSaveStateSuccess = await client.TrySaveStateAsync<Widget>(storeName, stateKeyName, updated, updatedETag);
            Console.WriteLine($"Saved state with latest etag: {updatedETag} successfully? {isSaveStateSuccess}");
        }

        private class Widget
        {
            public string? Size { get; set; }
            public string? Color { get; set; }
        }
    }
}
