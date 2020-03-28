// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents Dapr actor configuration for this app.  See
    /// https://github.com/dapr/docs/blob/master/reference/api/actors_api.md
    /// </summary>
    public sealed class ActorConfig
    {
        /// <summary>
        /// Default constructor.  This is only here for the unit test.
        /// </summary>
        private ActorConfig()
            : this(null, null, null, false)
        { }

        /// <summary>
        /// Users should use this constructor to override default values.
        /// </summary>
        /// <param name="actorIdleTimeout"></param>
        /// <param name="actorScanInterval"></param>
        /// <param name="drainOngoingCallTimeout"></param>
        /// <param name="drainBalancedActors"></param>
        public ActorConfig(
            TimeSpan? actorIdleTimeout = null,
            TimeSpan? actorScanInterval = null,
            TimeSpan? drainOngoingCallTimeout = default,
            bool drainBalancedActors = default)
        {
            this.RegisteredActorTypes = new List<string>();
            this.ActorIdleTimeout = actorIdleTimeout;
            this.ActorScanInterval = actorScanInterval;
            this.DrainOngoingCallTimeout = drainOngoingCallTimeout;
            this.DrainBalancedActors = drainBalancedActors;
        }

        /// <summary>
        /// The actor types to register with Dapr.
        /// </summary>
        public List<string> RegisteredActorTypes { get; private set; }

        /// <summary>
        /// Specifies how long to wait before deactivating an idle actor. An actor is idle 
        /// if no actor method calls and no reminders have fired on it.
        /// </summary>
        public TimeSpan? ActorIdleTimeout { get; set; }

        /// <summary>
        /// A duration which specifies how often to scan for actors to deactivate idle actors. 
        /// Actors that have been idle longer than the actorIdleTimeout will be deactivated.
        /// </summary>
        public TimeSpan? ActorScanInterval { get; set; }

        /// <summary>
        /// A duration used when in the process of draining rebalanced actors. This specifies 
        /// how long to wait for the current active actor method to finish. If there is no current actor method call, this is ignored.
        /// </summary>
        public TimeSpan? DrainOngoingCallTimeout { get; set; }

        /// <summary>
        /// A bool. If true, Dapr will wait for drainOngoingCallTimeout to allow a current 
        /// actor call to complete before trying to deactivate an actor. If false, do not wait.
        /// </summary>
        public bool DrainBalancedActors { get; set; }

        internal async Task SerializeAsync(System.Buffers.IBufferWriter<byte> output)
        {
            using Utf8JsonWriter writer = new Utf8JsonWriter(output);
            writer.WriteStartObject();

            // array 
            writer.WritePropertyName("entities");
            writer.WriteStartArray();

            foreach (var actorType in this.RegisteredActorTypes)
            {
                writer.WriteStringValue(actorType);
            }

            writer.WriteEndArray();

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

            // default is true, don't write it if default
            if (this.DrainBalancedActors != true)
            {
                writer.WriteBoolean("drainBalancedActors", (this.DrainBalancedActors));
            }

            writer.WriteEndObject();
            await writer.FlushAsync();
        }
    }
}
