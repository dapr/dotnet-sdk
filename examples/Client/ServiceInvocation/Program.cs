// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Client
{
    class Program
    {
        private static readonly Example[] Examples = new Example[]
        {
            new InvokeServiceGrpcExample(),
            new InvokeServiceHttpExample(),
            new InvokeServiceHttpClientExample(),
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
            return 1;
        }
    }
}
