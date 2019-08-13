// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    using System;
    using System.Runtime.Serialization;

    [DataContract(Name = "msgResponse", Namespace = Constants.ServiceCommunicationNamespace)]
    internal class ResponseMessageBody : IResponseMessageBody
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
