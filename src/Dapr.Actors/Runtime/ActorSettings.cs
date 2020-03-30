﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System;
    using System.Text.Json;

    /// <summary>
    /// Represents Dapr actor configuration for this app.  See
    /// https://github.com/dapr/docs/blob/master/reference/api/actors_api.md
    /// </summary>
    public sealed class ActorSettings
    {
        private TimeSpan? actorIdleTimeout;
        private TimeSpan? actorScanInterval;
        private TimeSpan? drainOngoingCallTimeout;
        private bool drainRebalancedActors;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ActorSettings()
        {
        }

        /// <summary>
        /// Specifies how long to wait before deactivating an idle actor. An actor is idle 
        /// if no actor method calls and no reminders have fired on it.
        /// See https://github.com/dapr/docs/blob/master/reference/api/actors_api.md#dapr-calling-to-user-service-code 
        /// for more including default values.
        /// </summary>
        public TimeSpan? ActorIdleTimeout
        {
            get
            {
                return this.actorIdleTimeout;
            }

            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(nameof(actorIdleTimeout), actorIdleTimeout, "must be positive");
                }

                this.actorIdleTimeout = value;
            }
        }

        /// <summary>
        /// A duration which specifies how often to scan for actors to deactivate idle actors. 
        /// Actors that have been idle longer than the actorIdleTimeout will be deactivated.
        /// See https://github.com/dapr/docs/blob/master/reference/api/actors_api.md#dapr-calling-to-user-service-code 
        /// for more including default values.
        /// </summary>
        public TimeSpan? ActorScanInterval
        {
            get
            {
                return this.actorScanInterval;
            }

            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(nameof(actorScanInterval), actorScanInterval, "must be positive");
                }

                this.actorScanInterval = value;
            }
        }

        /// <summary>
        /// A duration used when in the process of draining rebalanced actors. This specifies 
        /// how long to wait for the current active actor method to finish. If there is no current actor method call, this is ignored.
        /// See https://github.com/dapr/docs/blob/master/reference/api/actors_api.md#dapr-calling-to-user-service-code 
        /// for more including default values.
        /// </summary>
        public TimeSpan? DrainOngoingCallTimeout
        {
            get
            {
                return this.drainOngoingCallTimeout;
            }

            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(nameof(drainOngoingCallTimeout), drainOngoingCallTimeout, "must be positive");
                }

                this.drainOngoingCallTimeout = value;
            }
        }

        /// <summary>
        /// A bool. If true, Dapr will wait for drainOngoingCallTimeout to allow a current 
        /// actor call to complete before trying to deactivate an actor. If false, do not wait.
        /// See https://github.com/dapr/docs/blob/master/reference/api/actors_api.md#dapr-calling-to-user-service-code 
        /// for more including default values.
        /// </summary>
        public bool DrainRebalancedActors
        {
            get
            {
                return this.drainRebalancedActors;
            }

            set
            {
                this.drainRebalancedActors = value;
            }
        }
    }
}
