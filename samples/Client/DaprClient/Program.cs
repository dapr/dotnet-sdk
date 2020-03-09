// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace GrpcClient
{
    using System;
    using System.Threading.Tasks;
    using Google.Protobuf;
    using Google.Protobuf.WellKnownTypes;
    using Grpc.Net.Client;
    using Dapr.Client.Autogen.Grpc;
    using Dapr.Client;

    /// <summary>
    /// gRPC CLient sample class.
    /// </summary>
    public class Program
    {
        private static readonly string stateKeyName = "mykey";
        private static readonly string storeName = "statestore";

        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static async Task Main(string[] args)
        {
            var client = new DaprClientBuilder().Build();

            // Publish an event
            await PublishEventAsync(client);

            // Save State
            await SaveStateAsync(client);

            // Read State
            await GetStateAsync(client);

            // Delete State
            await DeleteStateAsync(client); 
        }

        internal static async Task PublishEventAsync(DaprClient client)
        {
            var eventData = new Widget() { Size = "small", Color = "yellow", };
            await client.PublishEventAsync("TopicA", eventData);
            Console.WriteLine("Published Event!");
        }

        internal static async Task SaveStateAsync(DaprClient client)
        {
            var state = new Widget() { Size = "small", Color = "yellow", };
            await client.SaveStateAsync(storeName, stateKeyName, state);
            Console.WriteLine("Saved State!");
        }

        internal static async Task GetStateAsync(DaprClient client)
        {
            var state = await client.GetStateAsync<Widget>(storeName, stateKeyName);
            if (state == null)
            {
                Console.WriteLine("State not found in store");
            }
            else
            {
                Console.WriteLine($"Got State: {state.Size}  {state.Color}");
            }
        }

        internal static async Task DeleteStateAsync(DaprClient client)
        {
            await client.DeleteStateAsync(storeName, stateKeyName);
            Console.WriteLine("Deleted State!");
        }

        internal static async Task InvokeMethodAsync(DaprClient client)
        {
            await client.InvokeMethodAsync("actor", "dapr/config");
            Console.WriteLine("Deleted State!");
        }

        private class Widget
        {
            public string Size { get; set; }
            public string Color { get; set; }
        }
    }
}
