// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

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
