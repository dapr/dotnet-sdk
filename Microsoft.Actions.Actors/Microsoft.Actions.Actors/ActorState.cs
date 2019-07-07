// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors
{
    /// <summary>
    /// Represents an ActorState.
    /// </summary>
    internal class ActorState<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActorState"/> class.
        /// </summary>
        /// <param name="key">The name of the actor state.</param>
        /// <param name="value">The value associated with given actor state name.</param>
        public ActorState(string key, T value)
        {
            this.Key = key;
            this.Value = value;
        }

        public string Key { get; }

        public T Value { get; }
    }
}
