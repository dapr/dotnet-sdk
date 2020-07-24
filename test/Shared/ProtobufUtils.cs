// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using System.Text.Json;
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

        public static ByteString ConvertToByteStringAsync<T>(T data, JsonSerializerOptions options = null)
        {
            if (data != null)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(data, options);
                return ByteString.CopyFrom(bytes);
            }

            return ByteString.Empty;
        }

        public static T ConvertFromAnyAsync<T>(Any any, JsonSerializerOptions options = null)
        {
            var utf8String = any.Value.ToStringUtf8();
            return JsonSerializer.Deserialize<T>(utf8String, options);
        }
    }
}
