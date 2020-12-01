// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
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

        internal ActorRuntime(ActorRuntimeOptions options, ILoggerFactory loggerFactory, ActorActivatorFactory activatorFactory)
        {
            this.options = options;
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.activatorFactory = activatorFactory;

            // Loop through actor registrations and create the actor manager for each one. 
            // We do this up front so that we can catch initialization errors early, and so
            // that access to state can have a simple threading model.
            // 
            // Revisit this if actor initialization becomes a significant source of delay for large projects.
            foreach (var actor in options.Actors)
            {
                this.actorManagers[actor.Type.ActorTypeName] = new ActorManager(
                    actor, 
                    actor.Activator ?? this.activatorFactory.CreateActivator(actor.Type), 
                    loggerFactory);
            }
        }

        /// <summary>
        /// Gets actor registrations registered with the runtime.
        /// </summary>
        public IReadOnlyList<ActorRegistration> RegisteredActors => this.options.Actors;

        internal static IDaprInteractor DaprInteractor => new DaprHttpInteractor();

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

            writer.WriteEndObject();
            return writer.FlushAsync();
        }

        /// <summary>
        /// Deactivates an actor for an actor type with given actor id.
        /// </summary>
        /// <param name="actorTypeName">Actor type name to deactivate the actor for.</param>
        /// <param name="actorId">Actor id for the actor to be deactivated.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        internal async Task DeactivateAsync(string actorTypeName, string actorId)
        {
            using(this.logger.BeginScope("ActorType: {ActorType}, ActorId: {ActorId}", actorTypeName, actorId))
            {
                await GetActorManager(actorTypeName).DeactivateActorAsync(new ActorId(actorId));
            }
        }

        /// <summary>
        /// Invokes the specified method for the actor when used with Remoting from CSharp client.
        /// </summary>
        /// <param name="actorTypeName">Actor type name to invokde the method for.</param>
        /// <param name="actorId">Actor id for the actor for which method will be invoked.</param>
        /// <param name="actorMethodName">Method name on actor type which will be invoked.</param>
        /// <param name="daprActorheader">Actor Header.</param>
        /// <param name="data">Payload for the actor method.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        internal Task<Tuple<string, byte[]>> DispatchWithRemotingAsync(string actorTypeName, string actorId, string actorMethodName, string daprActorheader, Stream data, CancellationToken cancellationToken = default)
        {
            using(this.logger.BeginScope("ActorType: {ActorType}, ActorId: {ActorId}, MethodName: {Reminder}", actorTypeName, actorId, actorMethodName))
            {
                return GetActorManager(actorTypeName).DispatchWithRemotingAsync(new ActorId(actorId), actorMethodName, daprActorheader, data, cancellationToken);
            }
        }

        /// <summary>
        /// Invokes the specified method for the actor when used without remoting, this is mainly used for cross language invocation.
        /// </summary>
        /// <param name="actorTypeName">Actor type name to invokde the method for.</param>
        /// <param name="actorId">Actor id for the actor for which method will be invoked.</param>
        /// <param name="actorMethodName">Method name on actor type which will be invoked.</param>
        /// <param name="requestBodyStream">Payload for the actor method.</param>
        /// <param name="responseBodyStream">Response for the actor method.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        internal Task DispatchWithoutRemotingAsync(string actorTypeName, string actorId, string actorMethodName, Stream requestBodyStream, Stream responseBodyStream, CancellationToken cancellationToken = default)
        {
            return GetActorManager(actorTypeName).DispatchWithoutRemotingAsync(new ActorId(actorId), actorMethodName, requestBodyStream, responseBodyStream, cancellationToken);
        }

        /// <summary>
        /// Fires a reminder for the Actor.
        /// </summary>
        /// <param name="actorTypeName">Actor type name to invokde the method for.</param>
        /// <param name="actorId">Actor id for the actor for which method will be invoked.</param>
        /// <param name="reminderName">The name of reminder provided during registration.</param>
        /// <param name="requestBodyStream">Payload for the actor method.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        internal Task FireReminderAsync(string actorTypeName, string actorId, string reminderName, Stream requestBodyStream, CancellationToken cancellationToken = default)
        {
            using(this.logger.BeginScope("ActorType: {ActorType}, ActorId: {ActorId}, ReminderName: {Reminder}", actorTypeName, actorId, reminderName))
            {
                return GetActorManager(actorTypeName).FireReminderAsync(new ActorId(actorId), reminderName, requestBodyStream, cancellationToken);
            }
        }

        /// <summary>
        /// Fires a timer for the Actor.
        /// </summary>
        /// <param name="actorTypeName">Actor type name to invokde the method for.</param>
        /// <param name="actorId">Actor id for the actor for which method will be invoked.</param>
        /// <param name="timerName">The name of timer provided during registration.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        internal Task FireTimerAsync(string actorTypeName, string actorId, string timerName, CancellationToken cancellationToken = default)
        {
            using(this.logger.BeginScope("ActorType: {ActorType}, ActorId: {ActorId}, TimerName: {Timer}", actorTypeName, actorId, timerName))
            {
                return GetActorManager(actorTypeName).FireTimerAsync(new ActorId(actorId), timerName, cancellationToken);
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
