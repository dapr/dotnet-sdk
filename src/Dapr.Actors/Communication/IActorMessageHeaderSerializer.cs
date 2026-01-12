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

namespace Dapr.Actors.Communication;

using System.IO;

/// <summary>
/// Represents a serializer that can serialize remoting layer message header to messaging layer header.
/// </summary>
internal interface IActorMessageHeaderSerializer
{
    /// <summary>
    ///  Serializes the remoting request message header to a message header.
    /// </summary>
    /// <param name="serviceRemotingRequestMessageHeader">Remoting header to serialize.</param>
    /// <returns>Serialized bytes.</returns>
    byte[] SerializeRequestHeader(IActorRequestMessageHeader serviceRemotingRequestMessageHeader);

    /// <summary>
    /// Deserializes a request message header in to remoting header.
    /// </summary>
    /// <param name="messageHeader">Messaging layer header to be deserialized.</param>
    /// <returns>An <see cref="IActorRequestMessageHeader"/> that has the deserialized contents of the specified message header.</returns>
    IActorRequestMessageHeader DeserializeRequestHeaders(Stream messageHeader);

    /// <summary>
    ///  Serializes the remoting response message header to a message header.
    /// </summary>
    /// <param name="serviceRemotingResponseMessageHeader">Remoting header to serialize.</param>
    /// <returns>Serialized bytes.</returns>
    byte[] SerializeResponseHeader(IActorResponseMessageHeader serviceRemotingResponseMessageHeader);

    /// <summary>
    /// Deserializes a response message header in to remoting header.
    /// </summary>
    /// <param name="messageHeader">Messaging layer header to be deserialized.</param>
    /// <returns>An <see cref="IActorRequestMessageHeader"/> that has the deserialized contents of the specified message header.</returns>
    IActorResponseMessageHeader DeserializeResponseHeaders(Stream messageHeader);
}