// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    // Note: using push-streaming content here has a little higher cost for trivially-size payloads,
    // but avoids the significant allocation overhead in the cases where the content is really large.
    //
    // Similar to https://github.com/aspnet/AspNetWebStack/blob/master/src/System.Net.Http.Formatting/PushStreamContent.cs
    // but simplified because of async.
    internal class AsyncJsonContent<T> : HttpContent
    {
        private readonly T obj;
        private readonly JsonSerializerOptions serializerOptions;

        public AsyncJsonContent(T obj, JsonSerializerOptions serializerOptions)
        {
            this.obj = obj;
            this.serializerOptions = serializerOptions;

            this.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "UTF-8", };
        }

        internal static AsyncJsonContent<T> CreateContent(T obj, JsonSerializerOptions serializerOptions)
        {
            return new AsyncJsonContent<T>(obj, serializerOptions);
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            return JsonSerializer.SerializeAsync(stream, this.obj, this.serializerOptions);
        }

        protected override bool TryComputeLength(out long length)
        {
            // We can't know the length of the content being pushed to the output stream without doing
            // some writing.
            //
            // If we want to optimize this case, it could be done by implementing a custom stream
            // and then doing the first write to a fixed-size pooled byte array.
            //
            // HTTP is slightly more efficient when you can avoid using chunking (need to know Content-Length)
            // up front.
            length = -1;
            return false;
        }
    }
}
