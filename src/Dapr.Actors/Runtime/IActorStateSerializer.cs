// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System;

    /// <summary>
    /// Represents a custom serializer for actor state.
    /// </summary>
    internal interface IActorStateSerializer
    {
        /// <summary>
        /// Deserializes from the given byte array to <typeparamref name="T"/>.
        /// </summary>
        /// <param name="buffer">The byte array to deserialize from.</param>
        /// <returns>The deserialized value.</returns>
        T Deserialize<T>(byte[] buffer);

        /// <summary>
        /// Serializes an object into a byte array.
        /// </summary>
        /// <param name="stateType">Type of the state.</param>
        /// <param name="state">The state value to serialize.</param>
        byte[] Serialize<T>(Type stateType, T state);
    }
}
