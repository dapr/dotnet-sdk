// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors
{
    /// <summary>
    /// Represents the configuration required for Actor Reentrancy.
    ///
    /// See: https://docs.dapr.io/developing-applications/building-blocks/actors/actor-reentrancy/
    /// </summary>
    public sealed class ActorReentrancyConfig 
    {
        private bool enabled;
        private int? maxStackDepth;

        /// <summary>
        /// Determines if Actor Reentrancy is enabled or disabled.
        /// </summary>
        public bool Enabled
        {
            get 
            {
                return this.enabled;
            }

            set 
            {
                this.enabled = value;
            }
        }

        /// <summary>
        /// Optional parameter that will stop a reentrant call from progressing past the defined
        /// limit. This is a safety measure against infinite reentrant calls.
        /// </summary>
        public int? MaxStackDepth
        {
            get
            {
                return this.maxStackDepth;
            }

            set
            {
                this.maxStackDepth = value;
            }
        }
    }
}