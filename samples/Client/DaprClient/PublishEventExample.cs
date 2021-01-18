// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;

namespace Samples.Client
{
    public class PublishEventExample : Example
    {
        private static readonly string pubsubName = "pubsub";

        public override string DisplayName => "Publishing Events";

        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            var client = new DaprClientBuilder().Build();

            var eventData = new { Id = "17", Amount = 10m, };
            await client.PublishEventAsync(pubsubName, "deposit", eventData, cancellationToken);
            Console.WriteLine("Published deposit event!");
        }

        private class Widget
        {
            public string? Size { get; set; }
            public string? Color { get; set; }
        }
    }
}
