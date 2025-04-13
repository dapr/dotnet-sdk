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

namespace Dapr.Actors.Client;

/// <summary>
/// Provides the interface for implementation of proxy access for actor service.
/// </summary>
public interface IActorProxy
{
    /// <summary>
    /// Gets <see cref="Dapr.Actors.ActorId"/> associated with the proxy object.
    /// </summary>
    /// <value><see cref="Dapr.Actors.ActorId"/> associated with the proxy object.</value>
    ActorId ActorId { get; }

    /// <summary>
    /// Gets actor implementation type of the actor associated with the proxy object.
    /// </summary>
    /// <value>Actor implementation type of the actor associated with the proxy object.</value>
    string ActorType { get; }
}