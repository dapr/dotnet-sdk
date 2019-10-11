// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Communication
{
    using System;
    using System.Runtime.Serialization;

    [DataContract(Name = "msgResponse", Namespace = Constants.Namespace)]
    internal class ActorResponseMessageBody : IActorResponseMessageBody
    {
        [DataMember]
        private object response;

        public void Set(object response)
        {
            this.response = response;
        }

        public object Get(Type paramType)
        {
            return this.response;
        }
    }
}
