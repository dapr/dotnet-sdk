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

using System;
using System.Collections.ObjectModel;

namespace Dapr.Actors.Runtime
{
    /// <summary>
    /// A collection of <see cref="ActorRegistration" /> instances.
    /// </summary>
    public sealed class ActorRegistrationCollection : KeyedCollection<ActorTypeInformation, ActorRegistration>
    {
        /// <summary>
        /// Returns the key for the item.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>The key.</returns>
        protected override ActorTypeInformation GetKeyForItem(ActorRegistration item)
        {
            return item.Type;
        }

        /// <summary>
        /// Registers an actor type in the collection.
        /// </summary>
        /// <typeparam name="TActor">Type of actor.</typeparam>
        /// <param name="configure">An optional delegate used to configure the actor registration.</param>
        public void RegisterActor<TActor>(Action<ActorRegistration> configure = null)
            where TActor : Actor
        {
            var actorTypeInfo = ActorTypeInformation.Get(typeof(TActor));
            var registration = new ActorRegistration(actorTypeInfo);
            configure?.Invoke(registration);
            this.Add(registration);
        }
    }
}
