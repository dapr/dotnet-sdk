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
    internal class ActorRequestMessageBody : IActorRequestMessageBody
    {
        [DataMember]
        private readonly Dictionary<string, object> parameters;

        public ActorRequestMessageBody(int parameterInfos)
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
    }
}
