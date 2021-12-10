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

namespace Dapr.Actors.Runtime
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.Actors.Client;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Contains methods to register actor types. Registering the types allows the runtime to create instances of the actor.
    /// </summary>
    public sealed class ActorRuntime
    {
        // Map of ActorType --> ActorManager.
        private readonly Dictionary<string, ActorManager> actorManagers = new Dictionary<string, ActorManager>();
        private readonly ActorRuntimeOptions options;
        private readonly ILogger logger;
        private readonly ActorActivatorFactory activatorFactory;
        private readonly IActorProxyFactory proxyFactory;

        internal ActorRuntime(ActorRuntimeOptions options, ILoggerFactory loggerFactory, ActorActivatorFactory activatorFactory, IActorProxyFactory proxyFactory)
        {
            this.options = options;
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.activatorFactory = activatorFactory;
            this.proxyFactory = proxyFactory;

            // Loop through actor registrations and create the actor manager for each one. 
            // We do this up front so that we can catch initialization errors early, and so
            // that access to state can have a simple threading model.
            // 
            // Revisit this if actor initialization becomes a significant source of delay for large projects.
            foreach (var actor in options.Actors)
            {
                var daprInteractor = new DaprHttpInteractor(clientHandler: null, httpEndpoint: options.HttpEndpoint, apiToken: options.DaprApiToken, requestTimeout: null);
                this.actorManagers[actor.Type.ActorTypeName] = new ActorManager(
                    actor,
                    actor.Activator ?? this.activatorFactory.CreateActivator(actor.Type),
                    this.options.JsonSerializerOptions,
                    loggerFactory, 
                    proxyFactory,
                    daprInteractor);
            }
        }

        /// <summary>
        /// Gets actor registrations registered with the runtime.
        /// </summary>
        public IReadOnlyList<ActorRegistration> RegisteredActors => this.options.Actors;

        internal Task SerializeSettingsAndRegisteredTypes(IBufferWriter<byte> output)
        {
            using Utf8JsonWriter writer = new Utf8JsonWriter(output);
            writer.WriteStartObject();

            writer.WritePropertyName("entities");
            writer.WriteStartArray();

            foreach (var actor in this.RegisteredActors)
            {
                writer.WriteStringValue(actor.Type.ActorTypeName);
            }

            writer.WriteEndArray();

            if (this.options.ActorIdleTimeout != null)
            {
                writer.WriteString("actorIdleTimeout", ConverterUtils.ConvertTimeSpanValueInDaprFormat(this.options.ActorIdleTimeout));
            }

            if (this.options.ActorScanInterval != null)
            {
                writer.WriteString("actorScanInterval", ConverterUtils.ConvertTimeSpanValueInDaprFormat(this.options.ActorScanInterval));
            }

            if (this.options.DrainOngoingCallTimeout != null)
            {
                writer.WriteString("drainOngoingCallTimeout", ConverterUtils.ConvertTimeSpanValueInDaprFormat(this.options.DrainOngoingCallTimeout));
            }

            // default is false, don't write it if default
            if (this.options.DrainRebalancedActors != false)
            {
                writer.WriteBoolean("drainRebalancedActors", (this.options.DrainRebalancedActors));
            }

            // default is null, don't write it if default
            if (this.options.RemindersStoragePartitions != null)
            {
                writer.WriteNumber("remindersStoragePartitions", this.options.RemindersStoragePartitions.Value);
            }

            // Reentrancy has a default value so it is always included.
            writer.WriteStartObject("reentrancy");            
            writer.WriteBoolean("enabled", this.options.ReentrancyConfig.Enabled);
            if (this.options.ReentrancyConfig.MaxStackDepth != null)
            {
                writer.WriteNumber("maxStackDepth", this.options.ReentrancyConfig.MaxStackDepth.Value);
            }
            writer.WriteEndObject();            

            writer.WriteEndObject();
            return writer.FlushAsync();
        }

        // Deactivates an actor for an actor type with given actor id.
        internal async Task DeactivateAsync(string actorTypeName, string actorId)
        {
            using(this.logger.BeginScope("ActorType: {ActorType}, ActorId: {ActorId}", actorTypeName, actorId))
            {
                await GetActorManager(actorTypeName).DeactivateActorAsync(new ActorId(actorId));
            }
        }

        // Invokes the specified method for the actor when used with Remoting from CSharp client.
        internal Task<Tuple<string, byte[]>> DispatchWithRemotingAsync(string actorTypeName, string actorId, string actorMethodName, string daprActorheader, Stream data, CancellationToken cancellationToken = default)
        {
            using(this.logger.BeginScope("ActorType: {ActorType}, ActorId: {ActorId}, MethodName: {Reminder}", actorTypeName, actorId, actorMethodName))
            {
                return GetActorManager(actorTypeName).DispatchWithRemotingAsync(new ActorId(actorId), actorMethodName, daprActorheader, data, cancellationToken);
            }
        }

        // Invokes the specified method for the actor when used without remoting, this is mainly used for cross language invocation.
        internal Task DispatchWithoutRemotingAsync(string actorTypeName, string actorId, string actorMethodName, Stream requestBodyStream, Stream responseBodyStream, CancellationToken cancellationToken = default)
        {
            return GetActorManager(actorTypeName).DispatchWithoutRemotingAsync(new ActorId(actorId), actorMethodName, requestBodyStream, responseBodyStream, cancellationToken);
        }

        // Fires a reminder for the Actor.
        internal Task FireReminderAsync(string actorTypeName, string actorId, string reminderName, Stream requestBodyStream, CancellationToken cancellationToken = default)
        {
            using(this.logger.BeginScope("ActorType: {ActorType}, ActorId: {ActorId}, ReminderName: {Reminder}", actorTypeName, actorId, reminderName))
            {
                return GetActorManager(actorTypeName).FireReminderAsync(new ActorId(actorId), reminderName, requestBodyStream, cancellationToken);
            }
        }

        // Fires a timer for the Actor.
        internal Task FireTimerAsync(string actorTypeName, string actorId, string timerName, Stream requestBodyStream, CancellationToken cancellationToken = default)
        {
            using(this.logger.BeginScope("ActorType: {ActorType}, ActorId: {ActorId}, TimerName: {Timer}", actorTypeName, actorId, timerName))
            {
                return GetActorManager(actorTypeName).FireTimerAsync(new ActorId(actorId), requestBodyStream, cancellationToken);
            }
        }

        private ActorManager GetActorManager(string actorTypeName)
        {
            if (!this.actorManagers.TryGetValue(actorTypeName, out var actorManager))
            {
                var errorMsg = $"Actor type {actorTypeName} is not registered with Actor runtime.";
                throw new InvalidOperationException(errorMsg);
            }

            return actorManager;
        }
    }
}
