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

        private ActorSettings actorSettings;

        private readonly ILogger logger;

        internal ActorRuntime(ActorRuntimeOptions options, ILoggerFactory loggerFactory)
        {
            this.actorSettings = new ActorSettings();
            this.logger = loggerFactory.CreateLogger(this.GetType());

            // Create ActorManagers, override existing entry if registered again.
            foreach(var actorServiceFunc in options.actorServicesFunc)
            {
                var actorServiceFactory = actorServiceFunc.Value ?? ((type) => new ActorService(type, loggerFactory));
                var actorService = actorServiceFactory.Invoke(actorServiceFunc.Key);

                this.actorManagers[actorServiceFunc.Key.ActorTypeName] = new ActorManager(actorService, loggerFactory);
            }
        }

        /// <summary>
        /// Gets actor type names registered with the runtime.
        /// </summary>
        public IEnumerable<string> RegisteredActorTypes => this.actorManagers.Keys;

        internal static IDaprInteractor DaprInteractor => new DaprHttpInteractor();


        /// <summary>
        /// Allows configuration of this app's actor configuration.
        /// </summary>
        /// <param name="actorSettingsDelegate">A delegate to edit the default ActorSettings object.</param>
        public void ConfigureActorSettings(Action<ActorSettings> actorSettingsDelegate)
        {
            actorSettingsDelegate.Invoke(this.actorSettings);
        }

        internal Task SerializeSettingsAndRegisteredTypes(IBufferWriter<byte> output)
        {
            using Utf8JsonWriter writer = new Utf8JsonWriter(output);
            writer.WriteStartObject();

            writer.WritePropertyName("entities");
            writer.WriteStartArray();

            foreach (var actorType in this.RegisteredActorTypes)
            {
                writer.WriteStringValue(actorType);
            }

            writer.WriteEndArray();

            if (this.actorSettings.ActorIdleTimeout != null)
            {
                writer.WriteString("actorIdleTimeout", ConverterUtils.ConvertTimeSpanValueInDaprFormat(this.actorSettings.ActorIdleTimeout));
            }

            if (this.actorSettings.ActorScanInterval != null)
            {
                writer.WriteString("actorScanInterval", ConverterUtils.ConvertTimeSpanValueInDaprFormat(this.actorSettings.ActorScanInterval));
            }

            if (this.actorSettings.DrainOngoingCallTimeout != null)
            {
                writer.WriteString("drainOngoingCallTimeout", ConverterUtils.ConvertTimeSpanValueInDaprFormat(this.actorSettings.DrainOngoingCallTimeout));
            }

            // default is false, don't write it if default
            if (this.actorSettings.DrainRebalancedActors != false)
            {
                writer.WriteBoolean("drainRebalancedActors", (this.actorSettings.DrainRebalancedActors));
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
                await GetActorManager(actorTypeName).DeactivateActor(new ActorId(actorId));
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
