// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Dapr.Actors.Communication
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
