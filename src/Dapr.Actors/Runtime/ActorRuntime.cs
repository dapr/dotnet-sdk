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

        // Deactivates an actor for an actor type with given actor id.
        internal async Task DeactivateAsync(string actorTypeName, string actorId)
        {
            using(this.logger.BeginScope("ActorType: {ActorType}, ActorId: {ActorId}", actorTypeName, actorId))
            {
                await GetActorManager(actorTypeName).DeactivateActor(new ActorId(actorId));
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
                return GetActorManager(actorTypeName).FireTimerAsync(new ActorId(actorId), timerName, requestBodyStream, cancellationToken);
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
