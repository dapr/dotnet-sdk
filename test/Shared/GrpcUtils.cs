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

namespace Dapr
{
    using System;
    using System.Buffers.Binary;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Google.Protobuf;
    using Grpc.Core;

    public class GrpcUtils
    {
        internal static readonly MediaTypeHeaderValue GrpcContentTypeHeaderValue = new MediaTypeHeaderValue("application/grpc");
        internal static readonly Version ProtocolVersion = new Version(2, 0);
        internal const string MessageEncodingHeader = "grpc-encoding";
        internal const string IdentityGrpcEncoding = "identity";
        internal const string StatusTrailer = "grpc-status";
        internal const int MessageDelimiterSize = 4; // how many bytes it takes to encode "Message-Length"
        internal const int HeaderSize = MessageDelimiterSize + 1; // message length + compression flag

        /// <summary>
        /// Gets the request from protobuf. 
        /// bytes in http request message content contains gRPC Headers and protobuf.
        /// https://github.com/grpc/grpc/blob/master/doc/PROTOCOL-HTTP2.md
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request"></param>
        /// <returns></returns>
        public static async Task<T> GetRequestFromRequestMessageAsync<T>(HttpRequestMessage request) where T : IMessage<T>, new()
        {
            var bytes = await request.Content.ReadAsByteArrayAsync();

            // first 5 bytes in request are grpc headers
            // https://github.com/grpc/grpc/blob/master/doc/PROTOCOL-HTTP2.md
            var parser = new MessageParser<T>(() => new T());
            var envelope = parser.ParseFrom(bytes[5..]);
            return envelope;
        }

        public static HttpResponseMessage CreateResponse(HttpStatusCode statusCode) =>
            CreateResponse(statusCode, string.Empty);

        public static HttpResponseMessage CreateResponse(HttpStatusCode statusCode, string payload) =>
            CreateResponse(statusCode, new StringContent(payload));

        public static HttpResponseMessage CreateResponse(
            HttpStatusCode statusCode,
            HttpContent payload,
            StatusCode? grpcStatusCode = StatusCode.OK,
            string grpcEncoding = null,
            Version version = null)
        {
            payload.Headers.ContentType = GrpcContentTypeHeaderValue;

            var message = new HttpResponseMessage(statusCode)
            {
                Content = payload,
                Version = version ?? ProtocolVersion
            };

            message.Headers.Add(MessageEncodingHeader, grpcEncoding ?? IdentityGrpcEncoding);

            if (grpcStatusCode != null)
            {
                message.TrailingHeaders.Add(StatusTrailer, grpcStatusCode.Value.ToString("D"));
            }

            return message;
        }

        public static Task<StreamContent> CreateResponseContent<TResponse>(TResponse response) where TResponse : IMessage<TResponse>
        {
            return CreateResponseContentCore(new[] { response });
        }

        private static async Task<StreamContent> CreateResponseContentCore<TResponse>(TResponse[] responses) where TResponse : IMessage<TResponse>
        {
            var ms = new MemoryStream();
            foreach (var response in responses)
            {
                await WriteResponseAsync(ms, response);
            }

            ms.Seek(0, SeekOrigin.Begin);
            var streamContent = new StreamContent(ms);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/grpc");
            return streamContent;
        }

        private static async Task WriteResponseAsync<TResponse>(Stream ms, TResponse response) where TResponse : IMessage<TResponse>
        {
            var data = response.ToByteArray();
            await WriteHeaderAsync(ms, data.Length, false);
            await ms.WriteAsync(data);
        }        

        private static Task WriteHeaderAsync(Stream stream, int length, bool compress = false)
        {
            var headerData = new byte[HeaderSize];

            // Compression flag
            headerData[0] = compress ? (byte)1 : (byte)0;

            // Message length
            EncodeMessageLength(length, headerData.AsSpan(1));

            return stream.WriteAsync(headerData, 0, headerData.Length);
        }

        private static void EncodeMessageLength(int messageLength, Span<byte> destination)
        {
            BinaryPrimitives.WriteUInt32BigEndian(destination, (uint)messageLength);
        }
    }
}
