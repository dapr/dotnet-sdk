// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using System.IO;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Google.Protobuf;
    using Google.Protobuf.WellKnownTypes;

    public class ProtobufUtils
    {
        public static async Task<Any> ConvertToAnyAsync<T>(T data, JsonSerializerOptions options = null)
        {
            using var stream = new MemoryStream();

            if (data != null)
            {
                await JsonSerializer.SerializeAsync(stream, data, options);
            }

            await stream.FlushAsync();

            // set the position to beginning of stream.
            stream.Seek(0, SeekOrigin.Begin);

            return new Any
            {
                Value = await ByteString.FromStreamAsync(stream)
            };
        }
    }
}
