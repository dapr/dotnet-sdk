// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

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
