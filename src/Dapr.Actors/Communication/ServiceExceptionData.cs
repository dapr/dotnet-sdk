// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Communication
{
    using System.Runtime.Serialization;
    using Dapr.Actors;

    [DataContract(Name = "ActorCommunicationExceptionData", Namespace = Constants.Namespace)]
    internal class ActorCommunicationExceptionData
    {
        public ActorCommunicationExceptionData(string type, string message)
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
