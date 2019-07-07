// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Runtime
{
    using System;

    /// <summary>
    /// Represents a change to an actor state with a given state name.
    /// </summary>
    public sealed class ActorStateChange
    {
        private readonly string stateName;
        private readonly Type type;
        private readonly object value;
        private readonly StateChangeKind changeKind;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorStateChange"/> class.
        /// </summary>
        /// <param name="stateName">The name of the actor state.</param>
        /// <param name="type">The type of value associated with given actor state name.</param>
        /// <param name="value">The value associated with given actor state name.</param>
        /// <param name="changeKind">The kind of state change for given actor state name.</param>
        public ActorStateChange(string stateName, Type type, object value, StateChangeKind changeKind)
        {
            ArgumentVerifier.ThrowIfNull(stateName, nameof(stateName));

            this.stateName = stateName;
            this.type = type;
            this.value = value;
            this.changeKind = changeKind;
        }

        /// <summary>
        /// Gets the name of the actor state.
        /// </summary>
        /// <value>
        /// The name of the actor state.
        /// </value>
        public string StateName
        {
            get { return this.stateName; }
        }

        /// <summary>
        /// Gets the type of value associated with given actor state name.
        /// </summary>
        /// <value>
        /// The type of value associated with given actor state name.
        /// </value>
        public Type Type
        {
            get { return this.type; }
        }

        /// <summary>
        /// Gets the value associated with given actor state name.
        /// </summary>
        /// <value>
        /// The value associated with given actor state name.
        /// </value>
        public object Value
        {
            get { return this.value; }
        }

        /// <summary>
        /// Gets the kind of state change for given actor state name.
        /// </summary>
        /// <value>
        /// The kind of state change for given actor state name.
        /// </value>
        public StateChangeKind ChangeKind
        {
            get { return this.changeKind; }
        }
    }
}
