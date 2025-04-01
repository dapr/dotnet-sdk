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

/// <summary>
/// Defines the interface that must be implemented to provide Request Message Body for remoting requests .
/// This contains all the parameters remoting method has.
/// </summary>
public interface IActorRequestMessageBody
{
    /// <summary>
    /// This Api gets called to set remoting method parameters before serializing/dispatching the request.
    /// </summary>
    /// <param name="position">Position of the parameter in Remoting Method.</param>
    /// <param name="parameName">Parameter Name in the Remoting Method.</param>
    /// <param name="parameter">Parameter Value.</param>
    void SetParameter(int position, string parameName, object parameter);

    /// <summary>
    /// This is used to retrive parameter from request body before dispatching to service remoting method.
    /// </summary>
    /// <param name="position">Position of the parameter in Remoting Method.</param>
    /// <param name="parameName">Parameter Name in the Remoting Method.</param>
    /// <param name="paramType">Parameter Type.</param>
    /// <returns>The parameter that is at the specified position and has the specified name.</returns>
    object GetParameter(int position, string parameName, Type paramType);
}