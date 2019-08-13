// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    using System;

    /// <summary>
    /// Defines the interface that must be implemented to provide Response Message Body for remoting requests .
    /// This contains the return Type of a remoting Method.
    /// </summary>
    public interface IResponseMessageBody
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
