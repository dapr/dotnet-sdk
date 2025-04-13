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

using System;
using System.Collections.Generic;

/// <summary>
/// Defines the interface that must be implemented for providing custom serialization for the remoting request.
/// </summary>
internal interface IActorMessageBodySerializationProvider
{
    /// <summary>
    /// Create a IServiceRemotingMessageBodyFactory used for creating remoting request and response body.
    /// </summary>
    /// <returns>A custom <see cref="IActorMessageBodyFactory"/> that can be used for creating remoting request and response message bodies.</returns>
    IActorMessageBodyFactory CreateMessageBodyFactory();

    /// <summary>
    /// Creates IActorRequestMessageBodySerializer for a serviceInterface using Wrapped Message DataContract implementation.
    /// </summary>
    /// <param name="serviceInterfaceType">The remoted service interface.</param>
    /// <param name="methodRequestParameterTypes">The union of parameter types of all of the methods of the specified interface.</param>
    /// <param name="wrappedRequestMessageTypes">Wrapped Request Types for all Methods.</param>
    /// <returns>
    /// An instance of the <see cref="IActorRequestMessageBodySerializer" /> that can serialize the service
    /// actor request message body to a messaging body for transferring over the transport.
    /// </returns>
    IActorRequestMessageBodySerializer CreateRequestMessageBodySerializer(
        Type serviceInterfaceType,
        IEnumerable<Type> methodRequestParameterTypes,
        IEnumerable<Type> wrappedRequestMessageTypes = null);

    /// <summary>
    /// Creates IActorResponseMessageBodySerializer for a serviceInterface using Wrapped Message DataContract implementation.
    /// </summary>
    /// <param name="serviceInterfaceType">The remoted service interface.</param>
    /// <param name="methodReturnTypes">The return types of all of the methods of the specified interface.</param>
    /// <param name="wrappedResponseMessageTypes">Wrapped Response Types for all remoting methods.</param>
    /// <returns>
    /// An instance of the <see cref="IActorResponseMessageBodySerializer" /> that can serialize the service
    /// actor response message body to a messaging body for transferring over the transport.
    /// </returns>
    IActorResponseMessageBodySerializer CreateResponseMessageBodySerializer(
        Type serviceInterfaceType,
        IEnumerable<Type> methodReturnTypes,
        IEnumerable<Type> wrappedResponseMessageTypes = null);
}