// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace GrpcClient
{
    using System;
    using System.Threading.Tasks;
    using Dapr.Client.Grpc;
    using Google.Protobuf;
    using Google.Protobuf.WellKnownTypes;
    using Grpc.Core;
    using Grpc.Net.Client;

    /// <summary>
    /// gRPC CLient sample class.
    /// </summary>
    public class Program
    {
        private static string stateKeyName = "mykey";

        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Arguments.</param>
        public static void Main(string[] args)
        {
            // Get default port from environment, the environment is set when launched by Dapr runtime.
            var defaultPort = Environment.GetEnvironmentVariable("DAPR_GRPC_PORT") ?? "52918";

            // Create Client
            var daprUri = $"127.0.0.1:{defaultPort}";
            var channel = new Channel(daprUri, ChannelCredentials.Insecure);
            var client = new Dapr.DaprClient(channel);

            // Publish an event
            PublishEventAsync(client).GetAwaiter().GetResult();

            // Save State
            SaveStateAsync(client).GetAwaiter().GetResult();

            // Read State
            GetStateAsync(client).GetAwaiter().GetResult();

            // Delete State
            DeleteStateAsync(client).GetAwaiter().GetResult();
        }

        private static async Task PublishEventAsync(Dapr.DaprClient client)
        {
            var data = new Any();
            data.Value = ByteString.CopyFromUtf8("EventData");

            // Create PublishEventEnvelope
            var eventToPublish = new PublishEventEnvelope()
            {
                Topic = "TopicA",
                Data = data,
            };
            _ = await client.PublishEventAsync(eventToPublish);
            Console.WriteLine("Published Event!");
        }

        private static async Task SaveStateAsync(Dapr.DaprClient client)
        {
            var value = new Any();
            value.Value = ByteString.CopyFromUtf8("my data");
            var req = new StateRequest()
            {
                Key = stateKeyName,
                Value = value,
            };

            var saveStateEnvelope = new SaveStateEnvelope();
            saveStateEnvelope.Requests.Add(req);
            _ = await client.SaveStateAsync(saveStateEnvelope);
            Console.WriteLine("Saved State!");
        }

        private static async Task GetStateAsync(Dapr.DaprClient client)
        {
            var getStateEnvelope = new GetStateEnvelope()
            {
                Key = stateKeyName,
            };

            var response = await client.GetStateAsync(getStateEnvelope);
            Console.WriteLine("Got State: " + response.Data.Value.ToStringUtf8());
        }

        private static async Task DeleteStateAsync(Dapr.DaprClient client)
        {
            var deleteStateEnvelope = new DeleteStateEnvelope()
            {
                Key = stateKeyName,
            };

            _ = await client.DeleteStateAsync(deleteStateEnvelope);
            Console.WriteLine("Deleted State!");
        }
    }
}
