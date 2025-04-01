// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr.Actors.Communication;

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