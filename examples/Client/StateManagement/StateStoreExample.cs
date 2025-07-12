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
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;

namespace Samples.Client;

public class StateStoreExample : Example
{
    private static readonly string stateKeyName = "widget";
    private static readonly string storeName = "statestore";

    public override string DisplayName => "Using the State Store";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        using var client = new DaprClientBuilder().Build();

        var state = new Widget() { Size = "small", Color = "yellow", };
        await client.SaveStateAsync(storeName, stateKeyName, state, cancellationToken: cancellationToken);
        Console.WriteLine("Saved State!");

        state = await client.GetStateAsync<Widget>(storeName, stateKeyName, cancellationToken: cancellationToken);
        if (state == null)
        {
            Console.WriteLine("State not found in store");
        }
        else
        {
            Console.WriteLine($"Got State: {state.Size} {state.Color}");
        }

        await client.DeleteStateAsync(storeName, stateKeyName, cancellationToken: cancellationToken);
        Console.WriteLine("Deleted State!");
    }

    private class Widget
    {
        public string? Size { get; set; }
        public string? Color { get; set; }
    }
}