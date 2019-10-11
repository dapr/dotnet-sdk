// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Communication
{
    /// <summary>
    /// Defines the interface that must be implemented for providing factory for creating actor request body and response body objects.
    /// </summary>
    public interface IActorMessageBodyFactory
    {
        /// <summary>
        /// Creates a actor request message body.
        /// </summary>
        /// <param name="interfaceName"> This is FullName for the service interface for which request body is being constructed.</param>
        /// <param name="methodName">MethodName for the service interface for which request will be sent to.</param>
        /// <param name="numberOfParameters">Number of Parameters in that Method.</param>
        /// <param name="wrappedRequestObject">Wrapped Request Object.</param>
        /// <returns>IActorRequestMessageBody.</returns>
        IActorRequestMessageBody CreateRequestMessageBody(string interfaceName, string methodName, int numberOfParameters, object wrappedRequestObject);

        /// <summary>
        /// Creates a actor response message body.
        /// </summary>
        /// <param name="interfaceName"> This is FullName for the service interface for which request body is being constructed.</param>
        /// <param name="methodName">MethodName for the service interface for which request will be sent to.</param>
        /// <param name="wrappedResponseObject">Wrapped Response Object.</param>
        /// <returns>IActorResponseMessageBody.</returns>
        IActorResponseMessageBody CreateResponseMessageBody(string interfaceName, string methodName, object wrappedResponseObject);
    }
}
