// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Communication
{
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
}
