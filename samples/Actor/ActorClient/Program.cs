// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace ActorClient
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using Dapr.Actors;
    using Dapr.Actors.Client;
    using IDemoActorInterface;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Actor Client class.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="args">Arguments.</param>
        public static void Main(string[] args)
        {
            var data = new MyData()
            {
                PropertyA = "ValueA",
                PropertyB = "ValueB",
            };

            // Create an actor Id.
            var actorId = new ActorId("abc");

            // Make strongly typed Actor calls with Remoting.
            // DemoACtor is the type registered with Dapr runtime in the service.
            var proxy = ActorProxy.Create<IDemoActor>(actorId, "DemoActor");
            Console.WriteLine("Making call using actor proxy to save data.");
            proxy.SaveData(data).GetAwaiter().GetResult();
            Console.WriteLine("Making call using actor proxy to get data.");
            var receivedData = proxy.GetData().GetAwaiter().GetResult();
            Console.WriteLine($"Received data is {receivedData.ToString()}");
        }
    }
}
