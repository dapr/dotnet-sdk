// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Runtime.Serialization;
using Dapr.Actors;
using Dapr.Actors.Communication;

[DataContract(Name = "WrappedMsgBody", Namespace = Constants.Namespace)]
internal class WrappedMessageBody : WrappedMessage, IActorRequestMessageBody, IActorResponseMessageBody
{
    public void SetParameter(
          int position,
          string parameName,
          object parameter)
    {
        throw new NotImplementedException();
    }

    public object GetParameter(
        int position,
        string parameName,
        Type paramType)
    {
        throw new NotImplementedException();
    }

    public void Set(
        object response)
    {
        throw new NotImplementedException();
    }

    public object Get(
        Type paramType)
    {
        throw new NotImplementedException();
    }
}
