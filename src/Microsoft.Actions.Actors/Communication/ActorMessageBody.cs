// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    [DataContract(Name = "msgBody", Namespace = Constants.Namespace)]
    internal class ActorMessageBody : IActorMessageBody
    {
        [DataMember]
        private Dictionary<string, object> parameters;

        [DataMember]
        private object response;

        public ActorMessageBody(int parameterInfos)
        {
            this.parameters = new Dictionary<string, object>(parameterInfos);
        }

        public void SetParameter(int position, string paramName, object parameter)
        {
            this.parameters[paramName] = parameter;
        }

        public object GetParameter(int position, string paramName, Type paramType)
        {
            return this.parameters[paramName];
        }

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
