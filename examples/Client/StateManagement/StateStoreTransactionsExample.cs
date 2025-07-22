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
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;

namespace Samples.Client;

public class StateStoreTransactionsExample : Example
{
    private static readonly string storeName = "statestore";

    public override string DisplayName => "Using the State Store with Transactions";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        using var client = new DaprClientBuilder().Build();

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