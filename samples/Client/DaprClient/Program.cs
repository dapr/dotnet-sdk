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
            var client = new DaprClientBuilder().Build();                     

            await PublishEventAsync(client);

            // Save State
            await SaveStateAsync(client);

            // Read State
            await GetStateAsync(client);

            // Delete State
            await DeleteStateAsync(client);

            #region Service Invoke - Required RoutingService
            // This provides an example of how to invoke a method on another REST service that is listening on http.
            // To use it run RoutingService in this solution.
            // Invoke deposit operation on RoutingSample service by publishing event.
            //await PublishDepositeEventToRoutingSampleAsync(client);

            // Invoke deposit operation on RoutingSample service by POST.
            //await InvokeWithdrawServiceOperationAsync(client);

            // Invoke deposit operation on RoutingSample service by GET.
            //await InvokeBalanceServiceOperationAsync(client);
            #endregion

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
        /// This example shows how to invoke a POST method on a service listening on http.
        /// Example:  curl -X POST http://127.0.0.1:5000/deposit -H "Content-Type: application/json" -d "{ \"id\": \"17\", \"amount\": 120 }"
        /// </summary>
        /// <param name="client"></param>
        /// <remarks>
        /// Before invoking this method, please first run RoutingSample.
        /// </remarks>
        /// <returns></returns>
        internal static async Task InvokeWithdrawServiceOperationAsync(DaprClient client)
        {
            var data = new { id = "17", amount = (decimal)10, };

            // Add the verb to metadata if the method is other than a POST
            var metaData = new Dictionary<string, string>();
            metaData.Add("http.verb", "POST");

            // Invokes a POST method named "Withdraw" that takes input of type "Transaction" as define in the RoutingSample.
            await client.InvokeMethodAsync<object>("routing", "Withdraw", data, metaData);

            Console.WriteLine("Completed");
        }

        /// <summary>
        /// This example shows how to invoke a GET method on a service listening on http.
        /// Example:  curl -X GET http://127.0.0.1:5000/17
        /// </summary>
        /// <param name="client"></param>
        /// <remarks>
        /// Before invoking this method, please first run RoutingSample.
        /// </remarks>
        /// <returns></returns>
        internal static async Task InvokeBalanceServiceOperationAsync(DaprClient client)
        {
           // Add the verb to metadata if the method is other than a POST
            var metaData = new Dictionary<string, string>();
            metaData.Add("http.verb", "GET");

            // Invokes a GET method named "hello" that takes input of type "MyData" and returns a string.
            var res = await client.InvokeMethodAsync<object>("routing", "17", metaData);
           
            Console.WriteLine($"Received balance {res}");
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
