using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;

namespace Samples.Client;

public class BulkStateExample : Example
{
    private static readonly string firstKey = "testKey1";
    private static readonly string secondKey = "testKey2";
    private static readonly string firstEtag = "123";
    private static readonly string secondEtag = "456";
    private static readonly string storeName = "statestore";

    public override string DisplayName => "Using the State Store";
        
    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        using var client = new DaprClientBuilder().Build();

        var state1 = new Widget() { Size = "small", Color = "yellow", };
        var state2 = new Widget() { Size = "big", Color = "green", };
            
        var stateItem1 = new SaveStateItem<Widget>(firstKey, state1, firstEtag);
        var stateItem2 = new SaveStateItem<Widget>(secondKey, state2, secondEtag);

        await client.SaveBulkStateAsync(storeName, new List<SaveStateItem<Widget>>() { stateItem1, stateItem2});
            
        Console.WriteLine("Saved 2 States!");
            
        await Task.Delay(2000);

        IReadOnlyList<BulkStateItem> states = await client.GetBulkStateAsync(storeName, 
            new List<string>(){firstKey, secondKey}, null);

        Console.WriteLine($"Got {states.Count} States: ");
         
        var deleteBulkStateItem1 = new BulkDeleteStateItem(states[0].Key, states[0].ETag);
        var deleteBulkStateItem2 = new BulkDeleteStateItem(states[1].Key, states[1].ETag);
            
        await client.DeleteBulkStateAsync(storeName, new List<BulkDeleteStateItem>() { deleteBulkStateItem1, deleteBulkStateItem2 });

        Console.WriteLine("Deleted States!");
    }
        
    private class Widget
    {
        public string? Size { get; set; }
        public string? Color { get; set; }
    }
}