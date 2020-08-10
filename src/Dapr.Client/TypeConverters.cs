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
    /// Some type converters.
    /// </summary>
    public static class TypeConverters
    {
        /// <summary>
        /// Converts an arbitrary type to a <see cref="System.Text.Json"/> based <see cref="ByteString"/>.
        /// </summary>
        /// <param name="data">The data to convert.</param>
        /// <param name="options">The JSON serialization options.</param>
        /// <typeparam name="T">The type of the given data.</typeparam>
        /// <returns>The given data as JSON based byte string.</returns>
        public static ByteString ToJsonByteString<T>(T data, JsonSerializerOptions options = null)
        {
            if (data == null)
            {
                return ByteString.Empty;
            }

            var bytes = JsonSerializer.SerializeToUtf8Bytes(data, options);
            return ByteString.CopyFrom(bytes);
        }

        /// <summary>
        /// Converts an arbitrary type to the <see cref="Any"/> Protocol Buffer type.
        ///
        /// If the given type is a subtype of <see cref="IMessage"/>, then it's save to use the Protocol Buffer
        /// packaging provided for the <see cref="Any"/> type with the <see cref="Any.Pack(Google.Protobuf.IMessage)"/>.
        /// For all other types, we use JSON serialization based on <see cref="System.Text.Json"/>.
        /// </summary>
        /// <param name="data">The data to convert.</param>
        /// <param name="options">The JSON serialization options.</param>
        /// <typeparam name="T">The type of the given data.</typeparam>
        /// <returns>The <see cref="Any"/> type representation of the given data.</returns>
        public static Any ToAny<T>(T data, JsonSerializerOptions options = null)
        {
            if (data == null)
            {
                return new Any();
            }

            var t = typeof(T);

            return typeof(IMessage).IsAssignableFrom(t)
                ? Any.Pack((IMessage) data)
                : new Any {Value = ToJsonByteString(data, options), TypeUrl = t.FullName};
        }

        /// <summary>
        /// Converts the Protocol Buffer <see cref="Any"/> type to an arbitrary type.
        ///
        /// If the type to convert to is a subtype of <see cref="IMessage"/> and if the type has an empty default
        /// constructor, then we use the <see cref="Any.Unpack{T}"/> method to deserialize the given <see cref="Any"/>
        /// instance. For all other types, we use JSON deserialization based on <see cref="System.Text.Json"/>.
        /// </summary>
        /// <param name="any">The any instance to convert.</param>
        /// <param name="options">The JSON serialization options.</param>
        /// <typeparam name="T">The type to convert to.</typeparam>
        /// <returns>An instance of T.</returns>
        public static T FromAny<T>(Any any, JsonSerializerOptions options = null)
        {
            var t = typeof(T);

            if (typeof(IMessage).IsAssignableFrom(t) && t.GetConstructor(System.Type.EmptyTypes) != null)
            {
                var method = any.GetType().GetMethod("Unpack");
                var generic = method.MakeGenericMethod(t);
                return (T) generic.Invoke(any, null);
            }

            return JsonSerializer.Deserialize<T>(any.Value.ToStringUtf8(), options);
        }
    }
}
