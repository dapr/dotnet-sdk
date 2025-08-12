// ------------------------------------------------------------------------
// Copyright 2023 The Dapr Authors
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

namespace Samples.Client;

class Program
{
    private static readonly Example[] Examples = new Example[]
    {
        new PublishEventExample(),
        new PublishBytesExample(),
    };

    static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && int.TryParse(args[0], out var index) && index >= 0 && index < Examples.Length)
        {
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) => cts.Cancel();

            await Examples[index].RunAsync(cts.Token);
            return 0;
        }

        Console.WriteLine("Hello, please choose a sample to run:");
        for (var i = 0; i < Examples.Length; i++)
        {
            Console.WriteLine($"{i}: {Examples[i].DisplayName}");
        }
        Console.WriteLine();
        return 0;
    }
}