// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;

namespace Samples.Client
{
    public class StateStoreTransactionsExample : Example
    {
        private static readonly string storeName = "statestore";

        public override string DisplayName => "Using the State Store with Transactions";

        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            var client = new DaprClientBuilder().Build();

            var value = new Widget() { Size = "small", Color = "yellow", };

            // State transactions operate on raw bytes
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value);

            var requests = new List<StateTransactionRequest>()
            {
                new StateTransactionRequest("widget", bytes, StateOperationType.Upsert),
                new StateTransactionRequest("oldwidget", null, StateOperationType.Delete)
            };
            
            Console.WriteLine("Executing transaction - save state and delete state");
            await client.ExecuteStateTransactionAsync(storeName, requests, cancellationToken: cancellationToken);
            Console.WriteLine("Executed State Transaction!");

            var state = await client.GetStateAsync<Widget>(storeName, "widget", cancellationToken: cancellationToken);
            if (state == null)
            {
                Console.WriteLine("State not found in store");
            }
            else
            {
                Console.WriteLine($"Got State: {state.Size} {state.Color}");
            }
        }

        private class Widget
        {
            public string? Size { get; set; }
            public string? Color { get; set; }
        }
    }
}
