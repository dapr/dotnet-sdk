// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Dapr.Actors.Communication
{
    using System;

    /// <summary>
    /// Defines the interface that must be implemented to provide Request Message Body for remoting requests .
    /// This contains all the parameters remoting method has.
    /// </summary>
    public interface IActorResponseMessageBody
    {
        /// <summary>
        /// Sets the response of a remoting Method in a remoting response Body.
        /// </summary>
        /// <param name="response">Remoting Method Response.</param>
        void Set(object response);

        /// <summary>
        /// Gets the response of a remoting Method from a remoting response body before sending it to Client.
        /// </summary>
        /// <param name="paramType"> Return Type of a Remoting Method.</param>
        /// <returns>Remoting Method Response.</returns>
        object Get(Type paramType);
    }
}
