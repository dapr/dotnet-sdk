// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace GrpcExample
{
    using System;

    using Dapr.Grpc.Client;
    using Google.Protobuf;
    using Google.Protobuf.WellKnownTypes;
    using Grpc.Core;

    #pragma warning disable
    public class Program
    {
        public static void Main(string[] args)
        {
            Channel channel = new Channel("127.0.0.1:50001", ChannelCredentials.Insecure);

            var client = new Dapr.DaprClient(channel);

            var evt = new PublishEventEnvelope();
            evt.Topic = "foo";
            var data = new Any();
            data.Value = ByteString.CopyFromUtf8("lala");
            evt.Data = data;
            client.PublishEvent(evt);
            Console.WriteLine("Published!");

            var key = "mykey";
            var req = new StateRequest();
            req.Key = key;
            var value = new Any();
            value.Value = ByteString.CopyFromUtf8("my data");
            req.Value = value;
            var state = new SaveStateEnvelope();
            state.Requests.Add(req);
            client.SaveState(state);
            Console.WriteLine("Saved!");

            var get = new GetStateEnvelope();
            get.Key = key;
            var resp = client.GetState(get);
            Console.WriteLine("Got: " + resp.Data.Value.ToStringUtf8());

            var delete = new DeleteStateEnvelope();
            delete.Key = key;
            client.DeleteState(delete);
            Console.WriteLine("Deleted!");
        }
    }
    #pragma warning enable
}