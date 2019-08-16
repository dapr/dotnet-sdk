// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    using System;

    /// <summary>
    /// Defines the interface that must be implemented to provide Request Message Body for remoting requests .
    /// This contains all the parameters remoting method has.
    /// </summary>
    public interface IActorMessageBody
    {
        /// <summary>
        /// This Api gets called to set remoting method parameters before serializing/dispatching the request.
        /// </summary>
        /// <param name="position">Position of the parameter in Remoting Method.</param>
        /// <param name="parameName">Parameter Name in the Remoting Method.</param>
        /// <param name="parameter">Parameter Value.</param>
        void SetParameter(int position, string parameName, object parameter);

        /// <summary>
        /// This is used to retrive parameter from request body before dispatching to service remoting method.
        /// </summary>
        /// <param name="position">Position of the parameter in Remoting Method.</param>
        /// <param name="parameName">Parameter Name in the Remoting Method.</param>
        /// <param name="paramType">Parameter Type.</param>
        /// <returns>The parameter that is at the specified position and has the specified name.</returns>
        object GetParameter(int position, string parameName, Type paramType);

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
