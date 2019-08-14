// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    /// <summary>
    /// Defines an interface that must be implemented to provide a serializer for Remoting Response Body.
    /// </summary>
    public interface IActorCommunicationResponseMessageBodySerializer
    {
        /// <summary>
        /// Serialize the remoting response body object to a message body that can be sent over the wire.
        /// </summary>
        /// <param name="serviceRemotingResponseMessageBody">Remoting response message body object.</param>
        /// <returns>Serialized message body.</returns>
        IOutgoingMessageBody Serialize(IResponseMessageBody serviceRemotingResponseMessageBody);

        /// <summary>
        /// Deserializes an incoming message body to remoting response body object.
        /// </summary>
        /// <param name="messageBody">Serialized message body.</param>
        /// <returns>Deserialized remoting response message body object.</returns>
        IResponseMessageBody Deserialize(IIncomingMessageBody messageBody);
    }
}
