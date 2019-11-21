// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System;

    /// <summary>
    /// Contains optional properties related to an actor implementation.
    /// </summary>
    /// <remarks>Intended to be attached to actor implementation types (i.e.those derived from <see cref="Actor" />).</remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ActorAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the name of the actor type represented by the actor.
        /// </summary>
        /// <value>The <see cref="string"/> name of the actor type represented by the actor.</value>
        /// <remarks>If set, this value will override the default actor type name derived from the actor implementation type.</remarks>
        public string TypeName { get; set; }
    }
}