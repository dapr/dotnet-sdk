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
public interface IActorResponseMessageBody
{
    /// <summary>
    /// Sets the response of a remoting Method in a remoting response Body.
    /// </summary>
    /// <param name="response">Remoting Method Response.</param>
    void Set(object response);

    /// <summary>
    /// Gets the response of a remoting Method from a remoting response body before sending it to Client.
    /// </summary>
    /// <param name="paramType"> Return Type of a Remoting Method.</param>
    /// <returns>Remoting Method Response.</returns>
    object Get(Type paramType);
}