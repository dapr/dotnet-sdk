using System;
using System.Collections.Generic;
using System.Text;
using Dapr.Client;
using System.Threading.Tasks;
using System.Threading;

namespace Samples.Client
{
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
            await client.SaveStateByteAsync(storeName, stateKeyName, stateBytes, cancellationToken: cancellationToken);
            Console.WriteLine("Saved State!");

            stateBytes = await client.GetStateByteAsync(storeName, stateKeyName, cancellationToken: cancellationToken);
            state = Encoding.UTF8.GetString(stateBytes);
            if (state == null)
            {
                Console.WriteLine("State not found in store");
            }
            else
            {
                Console.WriteLine($"Got State: {state}");
            }

            await client.DeleteStateAsync(storeName, stateKeyName, cancellationToken: cancellationToken);
            Console.WriteLine("Deleted State!");
        }

       
    }
}
