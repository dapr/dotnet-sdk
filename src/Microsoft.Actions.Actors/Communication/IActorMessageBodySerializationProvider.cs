// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
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
        /// Creates a serializer that can serialize and deserialize the remoting request message bodies for the specified service interface.
        /// </summary>
        /// <param name="serviceInterfaceType">User service interface.</param>
        /// <param name="requestWrappedTypes">Wrapped Request object Types for all method.</param>
        /// <param name="requestBodyTypes">Parameters for all the methods in the serviceInterfaceType.</param>
        /// <returns>
        /// An <see cref="IActorMessageBodySerializer"/> that can serialize and deserialize
        /// the remoting request message bodies created by the custom service remoting message body factory.
        /// </returns>
        IActorMessageBodySerializer CreateMessageBodySerializer(
            Type serviceInterfaceType,
            IEnumerable<Type> requestWrappedTypes,
            IEnumerable<Type> requestBodyTypes = null);
    }
}
