// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    using System.IO;

    /// <summary>
    /// Defines the interface that must be implemented to provide a serializer/deserializer for remoting request message body.
    /// </summary>
    public interface IActorMessageBodySerializer
    {
        /// <summary>
        /// Serialize the remoting request body object to a message body that can be sent over the wire.
        /// </summary>
        /// <param name="serviceRemotingRequestMessageBody">Remoting request message body object.</param>
        /// <returns>Serialized message body.</returns>
        byte[] Serialize(IActorMessageBody serviceRemotingRequestMessageBody);

        /// <summary>
        /// Deserializes an incoming message body to remoting request body object.
        /// </summary>
        /// <param name="messageBody">Serialized message body.</param>
        /// <returns>Deserialized remoting request message body object.</returns>
        IActorMessageBody Deserialize(Stream messageBody);
    }
}
