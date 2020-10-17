// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    using System.Text.Json;
    using Google.Protobuf;
    using Google.Protobuf.WellKnownTypes;

    /// <summary>
    /// A serializer that is able to serialize/deserialize into different formats which are used by
    /// the <see cref="DaprClientGrpc"/> implementation.
    /// </summary>
    public class GrpcSerializer
    {
        // property exposed for testing purposes
        internal JsonSerializerOptions JsonSerializerOptions { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GrpcSerializer"/> class.
        /// </summary>
        public GrpcSerializer()
        {
            JsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GrpcSerializer"/> class.
        /// </summary>
        /// <param name="jsonSerializerOptions">Json serialization options.</param>
        public GrpcSerializer(JsonSerializerOptions jsonSerializerOptions = null)
        {
            JsonSerializerOptions = jsonSerializerOptions;
        }

        /// <summary>
        /// Converts an arbitrary type to a <see cref="System.Text.Json"/> based <see cref="ByteString"/>.
        /// </summary>
        /// <param name="data">The data to convert.</param>
        /// <typeparam name="T">The type of the given data.</typeparam>
        /// <returns>The given data as JSON based byte string.</returns>
        public ByteString ToJsonByteString<T>(T data)
        {
            if (data == null)
            {
                return ByteString.Empty;
            }

            var bytes = JsonSerializer.SerializeToUtf8Bytes(data, JsonSerializerOptions);
            return ByteString.CopyFrom(bytes);
        }

        /// <summary>
        /// Converts a JSON byte string to an arbitrary type.
        /// </summary>
        /// <param name="json">The JSON byte string to convert.</param>
        /// <typeparam name="T">The type to convert to.</typeparam>
        /// <returns>An instance of T.</returns>
        public T FromJsonByteString<T>(ByteString json)
        {
            return JsonSerializer.Deserialize<T>(json.ToStringUtf8(), JsonSerializerOptions);
        }

        /// <summary>
        /// Converts an arbitrary type to the <see cref="Any"/> Protocol Buffer type.
        ///
        /// If the given type is a subtype of <see cref="IMessage"/>, then it's save to use the Protocol Buffer
        /// packaging provided for the <see cref="Any"/> type with the <see cref="Any.Pack(Google.Protobuf.IMessage)"/>.
        /// For all other types, we use JSON serialization based on <see cref="System.Text.Json"/>.
        /// </summary>
        /// <param name="data">The data to convert.</param>
        /// <typeparam name="T">The type of the given data.</typeparam>
        /// <returns>The <see cref="Any"/> type representation of the given data.</returns>
        public Any ToAny<T>(T data)
        {
            if (data == null)
            {
                return new Any();
            }

            var t = typeof(T);

            return typeof(IMessage).IsAssignableFrom(t)
                ? Any.Pack((IMessage) data)
                : new Any { Value = ToJsonByteString(data), TypeUrl = t.FullName };
        }

        /// <summary>
        /// Converts the Protocol Buffer <see cref="Any"/> type to an arbitrary type.
        ///
        /// If the type to convert to is a subtype of <see cref="IMessage"/> and if the type has an empty default
        /// constructor, then we use the <see cref="Any.Unpack{T}"/> method to deserialize the given <see cref="Any"/>
        /// instance. For all other types, we use JSON deserialization based on <see cref="System.Text.Json"/>.
        /// </summary>
        /// <param name="any">The any instance to convert.</param>
        /// <typeparam name="T">The type to convert to.</typeparam>
        /// <returns>An instance of T.</returns>
        public T FromAny<T>(Any any)
        {
            var t = typeof(T);

            if (!typeof(IMessage).IsAssignableFrom(t) || t.GetConstructor(System.Type.EmptyTypes) == null)
            {
                return FromJsonByteString<T>(any.Value);
            }

            var method = any.GetType().GetMethod("Unpack");
            var generic = method.MakeGenericMethod(t);

            return (T) generic.Invoke(any, null);
        }
    }
}
