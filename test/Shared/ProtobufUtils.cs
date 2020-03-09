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
        public static Any ConvertToAnyAsync<T>(T data, JsonSerializerOptions options = null)
        {
            var any = new Any();

            if (data != null)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(data, options);
                any.Value = ByteString.CopyFrom(bytes);

            }

            return any;
        }
    }
}
