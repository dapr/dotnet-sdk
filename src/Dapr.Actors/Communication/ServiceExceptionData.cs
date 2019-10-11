// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Communication
{
    using System.Runtime.Serialization;
    using Dapr.Actors;

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
