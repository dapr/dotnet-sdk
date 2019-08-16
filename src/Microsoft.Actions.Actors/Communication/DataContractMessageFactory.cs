// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    internal class DataContractMessageFactory : IActorMessageBodyFactory
    {
        public IActorMessageBody CreateMessageBody(string interfaceName, string methodName, object wrappedMessageObject, int numberOfParameters = 0)
        {
            return new ActorMessageBody(numberOfParameters);
        }
    }
}
