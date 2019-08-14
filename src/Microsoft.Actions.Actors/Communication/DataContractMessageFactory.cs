// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    internal class DataContractMessageFactory : IMessageBodyFactory
    {
        public IRequestMessageBody CreateRequest(string interfaceName, string methodName, int numberOfParameters, object wrappedRequest)
        {
            return new RequestMessageBody(numberOfParameters);
        }

        public IResponseMessageBody CreateResponse(string interfaceName, string methodName, object wrappedResponse)
        {
            return new ResponseMessageBody();
        }
    }
}
