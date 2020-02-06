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
    using Grpc.Net.Client;

    /// <summary>
    /// gRPC CLient sample class.
    /// </summary>
    public class Program
    {
        private static string stateKeyName = "mykey";
        private static string storeName = "statestore";

        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static async Task Main(string[] args)
        {
            // Get default port from environment, the environment is set when launched by Dapr runtime.
            var defaultPort = Environment.GetEnvironmentVariable("DAPR_GRPC_PORT") ?? "52918";

            // Set correct switch to make insecure gRPC service calls. This switch must be set before creating the GrpcChannel.
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            // Create Client
            var daprUri = $"http://127.0.0.1:{defaultPort}";
            var channel = GrpcChannel.ForAddress(daprUri);
            var client = new Dapr.DaprClient(channel);

            // Publish an event
            await PublishEventAsync(client);

            // Save State
            await SaveStateAsync(client);

            // Read State
            await GetStateAsync(client);

            // Delete State
            await DeleteStateAsync(client);
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
            saveStateEnvelope.StoreName = storeName;
            saveStateEnvelope.Requests.Add(req);
            _ = await client.SaveStateAsync(saveStateEnvelope);
            Console.WriteLine("Saved State!");
        }

        private static async Task GetStateAsync(Dapr.DaprClient client)
        {
            var getStateEnvelope = new GetStateEnvelope()
            {
                StoreName = storeName,
                Key = stateKeyName,
            };

            var response = await client.GetStateAsync(getStateEnvelope);
            Console.WriteLine("Got State: " + response.Data.Value.ToStringUtf8());
        }

        private static async Task DeleteStateAsync(Dapr.DaprClient client)
        {
            var deleteStateEnvelope = new DeleteStateEnvelope()
            {
                StoreName = storeName,
                Key = stateKeyName,
            };

            _ = await client.DeleteStateAsync(deleteStateEnvelope);
            Console.WriteLine("Deleted State!");
        }
    }
}
