// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace GrpcClient
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
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
            bool a = true;

            while (a)
            {
                Thread.Sleep(1000);
            }

            var client = new DaprClientBuilder().Build();

            await PublishDepositeEventToRoutingSampleAsync(client);

            await InvokeMethodOnHttpServiceAsync(client);

            await PublishEventAsync(client);

            // Save State
            await SaveStateAsync(client);

            // Read State
            await GetStateAsync(client);

            // Delete State
            await DeleteStateAsync(client);

            // This provides an example of how to invoke a method on another app that is listening on http.
            // This is commented out because it requires another app to be running.
            // await InvokeMethodOnHttpServiceAsync(client);
        }

        internal static async Task PublishDepositeEventToRoutingSampleAsync(DaprClient client)
        {
            var eventData = new  { id = "17", amount = (decimal)10, };
            await client.PublishEventAsync("deposit", eventData);
            Console.WriteLine("Published deposit event!");
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


        /// <summary>
        /// This example shows how to invoke a method on a service listening on http.
        /// In such a scenario, a key value pair of http.verb and the verb must be added to the metadata parameter.
        /// By default, Dapr uses POST as the verb if not specified/
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        internal static async Task InvokeMethodOnHttpServiceAsync(DaprClient client)
        {
            var data = new { id = "17", amount = (decimal)10, };

            // Add the verb to metadata if the method is other than a POST
            var metaData = new Dictionary<string, string>();
            metaData.Add("http.verb", "POST");

            // invokes a GET method named "Withdraw" that takes input of type "Transaction" as define in the RoutingSample.
            await client.InvokeMethodAsync<object>("bank", "Withdraw", data, metaData);

            Console.WriteLine("Completed");
        }


        internal static async Task InvokeWithdrawServiceAsync(DaprClient client)
        {
            MyData data = new MyData() { Message = "mydata" };

            // Add the verb to metadata if the method is other than a POST
            var metaData = new Dictionary<string, string>();
            metaData.Add("http.verb", "GET");

            // invokes a GET method named "hello" that takes input of type "MyData" and returns a string.
            string s = await client.InvokeMethodAsync<MyData, string>("nodeapp", "hello", data, metaData);
            Console.WriteLine("received {0}", s);
        }

        /// <summary>
        /// This example shows how to invoke a method on a service listening on gRPC.
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        internal static async Task InvokeMethodOnGrpcServiceAsync(DaprClient client)
        {
            MyData data = new MyData() { Message = "mydata" };

            // invokes a method named "hello" that takes input of type "MyData" and returns a string.
            string s = await client.InvokeMethodAsync<MyData, string>("nodeapp", "hello", data);
            Console.WriteLine("received {0}", s);
        }

        private class Widget
        {
            public string Size { get; set; }
            public string Color { get; set; }
        }

        class MyData
        {
            public MyData()
            { }

            public String Message { get; set; }
        }
    }
}
