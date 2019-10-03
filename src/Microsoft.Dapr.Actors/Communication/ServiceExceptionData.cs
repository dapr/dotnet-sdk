// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Dapr.Actors.Communication
{
    using System.Runtime.Serialization;
    using Microsoft.Dapr.Actors;

    [DataContract(Name = "ServiceExceptionData", Namespace = Constants.Namespace)]
    internal class ServiceExceptionData
    {
        public ServiceExceptionData(string type, string message)
        {
            this.Type = type;
            this.Message = message;
        }

        [DataMember]
        public string Type { get; private set; }

        [DataMember]
        public string Message { get; private set; }
    }
}
