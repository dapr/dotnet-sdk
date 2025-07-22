using System;
using System.Collections.Generic;
using System.Text;
using Dapr.Client;
using System.Threading.Tasks;
using System.Threading;
using Google.Protobuf;

namespace Samples.Client;

public class StateStoreBinaryExample : Example
{

    private static readonly string stateKeyName = "binarydata";
    private static readonly string storeName = "statestore";

    public override string DisplayName => "Using the State Store with binary data";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        using var client = new DaprClientBuilder().Build();

        var state = "Test Binary Data";
        // convert variable in to byte array
        var stateBytes = Encoding.UTF8.GetBytes(state);
        await client.SaveByteStateAsync(storeName, stateKeyName, stateBytes.AsMemory(), cancellationToken: cancellationToken);
        Console.WriteLine("Saved State!");

        var responseBytes = await client.GetByteStateAsync(storeName, stateKeyName, cancellationToken: cancellationToken);
        var savedState = Encoding.UTF8.GetString(ByteString.CopyFrom(responseBytes.Span).ToByteArray());
          
        if (savedState == null)
        {
            Console.WriteLine("State not found in store");
        }
        else
        {
            Console.WriteLine($"Got State: {savedState}");
        }

        await client.DeleteStateAsync(storeName, stateKeyName, cancellationToken: cancellationToken);
        Console.WriteLine("Deleted State!");
    }

       
}