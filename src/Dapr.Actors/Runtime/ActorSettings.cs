// ------------------------------------------------------------
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
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ActorSettings()
        {
        }

        /// <summary>
        /// Specifies how long to wait before deactivating an idle actor. An actor is idle 
        /// if no actor method calls and no reminders have fired on it.
        /// </summary>
        public TimeSpan? ActorIdleTimeout { get; private set; }

        /// <summary>
        /// A duration which specifies how often to scan for actors to deactivate idle actors. 
        /// Actors that have been idle longer than the actorIdleTimeout will be deactivated.
        /// </summary>
        public TimeSpan? ActorScanInterval { get; private set; }

        /// <summary>
        /// A duration used when in the process of draining rebalanced actors. This specifies 
        /// how long to wait for the current active actor method to finish. If there is no current actor method call, this is ignored.
        /// </summary>
        public TimeSpan? DrainOngoingCallTimeout { get; private set; }

        /// <summary>
        /// A bool. If true, Dapr will wait for drainOngoingCallTimeout to allow a current 
        /// actor call to complete before trying to deactivate an actor. If false, do not wait.
        /// </summary>
        public bool DrainRebalancedActors { get; private set; }

        /// <summary>
        /// Specifies how long to wait before deactivating an idle actor. An actor is idle 
        /// if no actor method calls and no reminders have fired on it.
        /// See https://github.com/dapr/docs/blob/master/reference/api/actors_api.md#dapr-calling-to-user-service-code 
        /// for more including default values.
        /// </summary>
        /// <param name="actorIdleTimeout">The value to set this parameter to.</param>
        public void SetActorIdleTimeout(TimeSpan actorIdleTimeout)
        {
            if (actorIdleTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(actorIdleTimeout), actorIdleTimeout, "must be positive");
            }

            this.ActorIdleTimeout = actorIdleTimeout;
        }

        /// <summary>
        /// A duration which specifies how often to scan for actors to deactivate idle actors. 
        /// Actors that have been idle longer than the actorIdleTimeout will be deactivated.
        /// See https://github.com/dapr/docs/blob/master/reference/api/actors_api.md#dapr-calling-to-user-service-code 
        /// for more including default values.
        /// </summary>
        /// <param name="actorScanInterval">The value to set this parameter to.</param>
        public void SetActorScanInterval(TimeSpan actorScanInterval)
        {
            if (actorScanInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(actorScanInterval), actorScanInterval, "must be positive");
            }

            this.ActorScanInterval = actorScanInterval;
        }

        /// <summary>
        /// A duration used when in the process of draining rebalanced actors. This specifies 
        /// how long to wait for the current active actor method to finish. If there is no current actor method call, this is ignored.
        /// See https://github.com/dapr/docs/blob/master/reference/api/actors_api.md#dapr-calling-to-user-service-code 
        /// for more including default values.
        /// </summary>
        /// <param name="drainOngoingCallTimeout">The value to set this parameter to.</param>
        public void SetDrainOngoingCallTimeout(TimeSpan drainOngoingCallTimeout)
        {
            if (drainOngoingCallTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(drainOngoingCallTimeout), drainOngoingCallTimeout, "must be positive");
            }

            this.DrainOngoingCallTimeout = drainOngoingCallTimeout;
        }

        /// <summary>
        /// A bool. If true, Dapr will wait for drainOngoingCallTimeout to allow a current 
        /// actor call to complete before trying to deactivate an actor. If false, do not wait.
        /// See https://github.com/dapr/docs/blob/master/reference/api/actors_api.md#dapr-calling-to-user-service-code 
        /// for more including default values.
        /// </summary>
        /// <param name="drainRebalancedActors">The value to set this parameter to.</param>
        public void SetDrainRebalancedActors(bool drainRebalancedActors)
        {
            this.DrainRebalancedActors = drainRebalancedActors;
        }

        internal void Serialize(System.Buffers.IBufferWriter<byte> output, Utf8JsonWriter writer)
        {
            if (this.ActorIdleTimeout != null)
            {
                writer.WriteString("actorIdleTimeout", ConverterUtils.ConvertTimeSpanValueInDaprFormat(this.ActorIdleTimeout));
            }

            if (this.ActorScanInterval != null)
            {
                writer.WriteString("actorScanInterval", ConverterUtils.ConvertTimeSpanValueInDaprFormat(this.ActorScanInterval));
            }

            if (this.DrainOngoingCallTimeout != null)
            {
                writer.WriteString("drainOngoingCallTimeout", ConverterUtils.ConvertTimeSpanValueInDaprFormat(this.DrainOngoingCallTimeout));
            }

            // default is false, don't write it if default
            if (this.DrainRebalancedActors != false)
            {
                writer.WriteBoolean("drainRebalancedActors", (this.DrainRebalancedActors));
            }
        }
    }
}
