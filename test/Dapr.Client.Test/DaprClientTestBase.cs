// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client.Test
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using Google.Protobuf;

    public class DaprClientTestBase
    {
        /// <summary>
        /// Gets the envelope from protobuf. 
        /// bytes in http request message content contains gRPC Headers and protobuf.
        /// https://github.com/grpc/grpc/blob/master/doc/PROTOCOL-HTTP2.md
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request"></param>
        /// <returns></returns>
        protected async Task<T> GetEnvelopeFromProtobufAsync<T>(HttpRequestMessage request) where T : IMessage<T>, new()
        {
            var bytes = await request.Content.ReadAsByteArrayAsync();

            // first 5 bytes in request are grpc headers
            // https://github.com/grpc/grpc/blob/master/doc/PROTOCOL-HTTP2.md
            var parser = new MessageParser<T>(() => new T());
            var envelope = parser.ParseFrom(bytes[5..]);
            return envelope;
        }
    }
}
