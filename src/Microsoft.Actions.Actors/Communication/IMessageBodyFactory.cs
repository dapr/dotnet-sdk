// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    /// <summary>
    /// Defines the interface that must be implemented for providing factory for creating remoting request body and response body objects.
    /// </summary>
    public interface IMessageBodyFactory
    {
        /// <summary>
        /// Creates a remoting request message body.
        /// </summary>
        /// <param name="interfaceName"> This is FullName for the service interface for which request body is being constructed.</param>
        /// <param name="methodName">MethodName for the service interface for which request will be sent to.</param>
        /// <param name="numberOfParameters">Number of Parameters in that Method.</param>
        /// <param name="wrappedRequestObject">Wrapped Request Object.</param>
        /// <returns>IRequestMessageBody.</returns>
        IRequestMessageBody CreateRequest(string interfaceName, string methodName, int numberOfParameters, object wrappedRequestObject);

        /// <summary>
        /// Creates a remoting response message body.
        /// </summary>
        /// <param name="interfaceName"> This is FullName for the service interface for which request body is being constructed.</param>
        /// <param name="methodName">MethodName for the service interface for which request will be sent to.</param>
        /// <param name="wrappedResponseObject">Wrapped Response Object.</param>
        /// <returns>IResponseMessageBody.</returns>
        IResponseMessageBody CreateResponse(string interfaceName, string methodName, object wrappedResponseObject);
    }
}
