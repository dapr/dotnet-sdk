// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Communication
{
    using System.IO;

    /// <summary>
    /// Defines the interface that must be implemented to provide a serializer/deserializer for actor response message body.
    /// </summary>
    internal interface IActorResponseMessageBodySerializer
    {
        /// <summary>
        /// Serialize the actor response body object to a message body that can be sent over the wire.
        /// </summary>
        /// <param name="actorResponseMessageBody">Actor request message body object.</param>
        /// <returns>Serialized message body.</returns>
        byte[] Serialize(IActorResponseMessageBody actorResponseMessageBody);

        /// <summary>
        /// Deserializes an incoming message body to remoting response body object.
        /// </summary>
        /// <param name="messageBody">Serialized message body.</param>
        /// <returns>Deserialized actor response message body object.</returns>
        IActorResponseMessageBody Deserialize(Stream messageBody);
    }
}
