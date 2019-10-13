// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Communication
{
    internal class DataContractMessageFactory : IActorMessageBodyFactory
    {
        public IActorRequestMessageBody CreateRequestMessageBody(string interfaceName, string methodName, int numberOfParameters, object wrappedRequestObject)
        {
            return new ActorRequestMessageBody(numberOfParameters);
        }

        public IActorResponseMessageBody CreateResponseMessageBody(string interfaceName, string methodName, object wrappedResponseObject)
        {
            return new ActorResponseMessageBody();
        }
    }
}
