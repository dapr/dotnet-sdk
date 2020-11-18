﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace DaprClient
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Dapr.Client;
    using Dapr.Client.Http;
    using Grpc.Core;

    /// <summary>
    /// Shows Dapr client calls.
    /// </summary>
    public class Program
    {
        private static readonly string stateKeyName = "mykey";
        private static readonly string storeName = "statestore";
        private static readonly string pubsubName = "pubsub";
        private static readonly string daprErrorInfoHTTPCodeMetadata  = "http.code";
        private static readonly string daprErrorInfoHTTPErrorMetadata = "http.error_message";
        private static readonly string grpcStatusDetails = "grpc-status-details-bin";
        private static readonly string grpcErrorInfoDetail = "google.rpc.ErrorInfo";

        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static async Task Main(string[] args)
        {
            var jsonOptions = new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
            };

            var client = new DaprClientBuilder()
                .UseJsonSerializationOptions(jsonOptions)
                .Build();

            await PublishEventAsync(client);

            // Save State
            await SaveStateAsync(client);

            // Read State
            await GetStateAsync(client);

            // Delete State
            await DeleteStateAsync(client);

            // State Transaction
            await ExecuteStateTransaction(client);

            // Read State
            await GetStateAfterTransactionAsync(client);

            if (args.Length > 0 && args[0] == "rpc-exception")
            {
                // Invoke /throwException route on the Controller sample server
                await InvokeThrowExceptionOperationAsync(client);
            }

            #region Service Invoke - Required RoutingService
            //// This provides an example of how to invoke a method on another REST service that is listening on http.
            //// To use it run RoutingService in this solution.
            //// Invoke deposit operation on RoutingSample service by publishing event.      

            //await PublishDepositeEventToRoutingSampleAsync(client);

            //await Task.Delay(TimeSpan.FromSeconds(1));

            //await DepositUsingServiceInvocation(client);

            ////Invoke deposit operation on RoutingSample service by POST.
            //await InvokeWithdrawServiceOperationAsync(client);

            ////Invoke deposit operation on RoutingSample service by GET.
            //await InvokeBalanceServiceOperationAsync(client);
            #endregion

            Console.WriteLine("Done");
        }

        internal static async Task PublishDepositeEventToRoutingSampleAsync(DaprClient client)
        {
            var eventData = new { Id = "17", Amount = (decimal)10, };
            await client.PublishEventAsync(pubsubName, "deposit", eventData);
            Console.WriteLine("Published deposit event!");
        }

        internal static async Task PublishEventAsync(DaprClient client)
        {
            var eventData = new Widget() { Size = "small", Color = "yellow", };            
            await client.PublishEventAsync(pubsubName, "TopicA", eventData);
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

        internal static async Task ExecuteStateTransaction(DaprClient client)
        {
            var value = new Widget() { Size = "small", Color = "yellow", }; 
            var request1 = new Dapr.StateTransactionRequest("mystate", JsonSerializer.SerializeToUtf8Bytes(value), StateOperationType.Upsert);
            var request2 = new Dapr.StateTransactionRequest("mystate", null, StateOperationType.Delete);
            var requests = new List<Dapr.StateTransactionRequest>();
            requests.Add(request1);
            requests.Add(request2);
            Console.WriteLine("Executing transaction - save state and delete state");
            await client.ExecuteStateTransactionAsync(storeName, requests);
            Console.WriteLine("Executed State Transaction!");
        }

        internal static async Task GetStateAfterTransactionAsync(DaprClient client)
        {
            var state = await client.GetStateAsync<Widget>(storeName, "mystate");
            if (state == null)
            {
                Console.WriteLine("State not found in store");
            }
            else
            {
                Console.WriteLine($"Got Transaction State: {state.Size} {state.Color}");
            }
        }

        internal static async Task DepositUsingServiceInvocation(DaprClient client)
        {
            Console.WriteLine("DepositUsingServiceInvocation");
            var data = new { id = "17", amount = (decimal)99 };

            // Invokes a POST method named "depoit" that takes input of type "Transaction" as define in the RoutingSample.
            Console.WriteLine("invoking");

            var a = await client.InvokeMethodAsync<object, Account>("routing", "deposit", data, HTTPExtension.UsingPost());
            Console.WriteLine("Returned: id:{0} | Balance:{1}", a.Id, a.Balance);

            Console.WriteLine("Completed");
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
            Console.WriteLine("Invoking withdraw");
            var data = new { id = "17", amount = (decimal)10, };

            // Invokes a POST method named "Withdraw" that takes input of type "Transaction" as define in the RoutingSample.            
            await client.InvokeMethodAsync<object>("routing", "Withdraw", data, HTTPExtension.UsingPost());

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
            Console.WriteLine("Invoking balance");

            // Invokes a GET method named "hello" that takes input of type "MyData" and returns a string.
            var res = await client.InvokeMethodAsync<Account>("routing", "17", HTTPExtension.UsingGet());

            Console.WriteLine($"Received balance {res.Balance}");
        }

        internal static async Task InvokeThrowExceptionOperationAsync(DaprClient client)
        {
            Console.WriteLine("Invoking ThrowException");
            var data = new { id = "17", amount = (decimal)10, };

            try
            {
                // Invokes a POST method named "throwException" that takes input of type "Transaction" as defined in the ControllerSample.            
                await client.InvokeMethodAsync("controller", "throwException", data, HTTPExtension.UsingPost());
            }
            catch (RpcException ex)
            {
                var entry = ex.Trailers.Get(grpcStatusDetails);
                var status = Google.Rpc.Status.Parser.ParseFrom(entry.ValueBytes);
                Console.WriteLine("Grpc Exception Message: " + status.Message);
                Console.WriteLine("Grpc Statuscode: " + status.Code);
                foreach(var detail in status.Details)
                {
                    if(Google.Protobuf.WellKnownTypes.Any.GetTypeName(detail.TypeUrl) == grpcErrorInfoDetail)
                    {
                        var rpcError = detail.Unpack<Google.Rpc.ErrorInfo>();
                        Console.WriteLine("Grpc Exception: Http Error Message: " + rpcError.Metadata[daprErrorInfoHTTPErrorMetadata]);
                        Console.WriteLine("Grpc Exception: Http Status Code: " + rpcError.Metadata[daprErrorInfoHTTPCodeMetadata]);
                    }
                }
            }
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


        internal class Account
        {
            public string Id { get; set; }

            public decimal Balance { get; set; }
        }
    }
}

