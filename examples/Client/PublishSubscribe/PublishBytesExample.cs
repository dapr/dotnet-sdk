using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;

namespace Samples.Client
{
    public class PublishBytesExample : Example
    {
        public override string DisplayName => "Publish Bytes";

        public async override Task RunAsync(CancellationToken cancellationToken)
        {
            using var client = new DaprClientBuilder().Build();

            var transaction = new { Id = "17", Amount = 30m };
            var content = JsonSerializer.SerializeToUtf8Bytes(transaction);

            await client.PublishByteEventAsync(pubsubName, "deposit", content.AsMemory(), MediaTypeNames.Application.Json, new Dictionary<string, string> { }, cancellationToken);
            Console.WriteLine("Published deposit event!");
        }
    }
}
