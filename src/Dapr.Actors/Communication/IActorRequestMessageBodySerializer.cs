// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Communication
{
    using System.IO;

    /// <summary>
    /// Defines the interface that must be implemented to provide a serializer/deserializer for remoting request message body.
    /// </summary>
    internal interface IActorRequestMessageBodySerializer
    {
        /// <summary>
        /// Serialize the remoting request body object to a message body that can be sent over the wire.
        /// </summary>
        /// <param name="actorRequestMessageBody">Actor request message body object.</param>
        /// <returns>Serialized message body.</returns>
        byte[] Serialize(IActorRequestMessageBody actorRequestMessageBody);

        /// <summary>
        /// Deserializes an incoming message body to actor request body object.
        /// </summary>
        /// <param name="messageBody">Serialized message body.</param>
        /// <returns>Deserialized remoting request message body object.</returns>
        IActorRequestMessageBody Deserialize(Stream messageBody);
    }
}
