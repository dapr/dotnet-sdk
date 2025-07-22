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

/// <summary>
/// Defines the interface that must be implemented for providing factory for creating actor request body and response body objects.
/// </summary>
public interface IActorMessageBodyFactory
{
    /// <summary>
    /// Creates a actor request message body.
    /// </summary>
    /// <param name="interfaceName"> This is FullName for the service interface for which request body is being constructed.</param>
    /// <param name="methodName">MethodName for the service interface for which request will be sent to.</param>
    /// <param name="numberOfParameters">Number of Parameters in that Method.</param>
    /// <param name="wrappedRequestObject">Wrapped Request Object.</param>
    /// <returns>IActorRequestMessageBody.</returns>
    IActorRequestMessageBody CreateRequestMessageBody(string interfaceName, string methodName, int numberOfParameters, object wrappedRequestObject);

    /// <summary>
    /// Creates a actor response message body.
    /// </summary>
    /// <param name="interfaceName"> This is FullName for the service interface for which request body is being constructed.</param>
    /// <param name="methodName">MethodName for the service interface for which request will be sent to.</param>
    /// <param name="wrappedResponseObject">Wrapped Response Object.</param>
    /// <returns>IActorResponseMessageBody.</returns>
    IActorResponseMessageBody CreateResponseMessageBody(string interfaceName, string methodName, object wrappedResponseObject);
}