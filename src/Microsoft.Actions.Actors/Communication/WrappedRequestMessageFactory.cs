// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Microsoft.Actions.Actors.Communication;

internal class WrappedRequestMessageFactory : IMessageBodyFactory
{
    public IRequestMessageBody CreateRequest(
        string interfaceName,
        string methodName,
        int numberOfParameters,
        object wrappedRequestObject)
    {
        return new WrappedMessageBody()
        {
            Value = wrappedRequestObject,
        };
    }

    public IResponseMessageBody CreateResponse(
        string interfaceName,
        string methodName,
        object wrappedResponseObject)
    {
        return new WrappedMessageBody()
        {
            Value = wrappedResponseObject,
        };
    }
}
