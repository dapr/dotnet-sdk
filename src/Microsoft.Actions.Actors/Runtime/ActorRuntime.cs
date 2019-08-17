// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Contains methods to register actor types. Registering the types allows the runtime to create instances of the actor.
    /// </summary>
    public class ActorRuntime
    {
        /// <summary>
        /// Gets ActorRuntime.
        /// </summary>
        public static readonly ActorRuntime Instance = new ActorRuntime();

        private const string TraceType = "ActorRuntime";

        // Map of ActorType --> ActorManager.
        private static Dictionary<string, ActorManager> actorManagers = new Dictionary<string, ActorManager>();

        private ActorRuntime()
        {
        }

        /// <summary>
        /// Gets actor type names registered with the runtime.
        /// </summary>
        public static IEnumerable<string> RegisteredActorTypes => actorManagers.Keys;

        /// <summary>
        /// Registers an actor with the runtime.
        /// </summary>
        /// <typeparam name="TActor">Type of actor.</typeparam>
        /// <param name="actorFactory">An optional delegate to create actor instances. This can be used for dependency injection into actors.</param>
        public void RegisterActor<TActor>(Func<ActorId, Actor> actorFactory = null)
            where TActor : Actor
        {
            var actorTypeName = typeof(TActor).Name;
            var actorTypeInfo = new ActorTypeInfo(typeof(TActor), actorFactory);

            // Create ActorManagers, override existing entry if registered again.
            actorManagers[actorTypeName] = new ActorManager(actorTypeInfo);
        }

        /// <summary>
        /// Activates an actor for an actor type with given actor id.
        /// </summary>
        /// <param name="actorTypeName">Actor type name to activate the actor for.</param>
        /// <param name="actorId">Actor id for the actor to be activated.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        internal static async Task ActivateAsync(string actorTypeName, string actorId)
        {
            await GetActorManager(actorTypeName).ActivateActor(new ActorId(actorId));
        }

        /// <summary>
        /// Deactivates an actor for an actor type with given actor id.
        /// </summary>
        /// <param name="actorTypeName">Actor type name to deactivate the actor for.</param>
        /// <param name="actorId">Actor id for the actor to be deactivated.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        internal static async Task DeactivateAsync(string actorTypeName, string actorId)
        {
            await GetActorManager(actorTypeName).DeactivateActor(new ActorId(actorId));
        }

        /// <summary>
        /// Invokes the specified method for the actor when used with strongly typed invocaton from C Sharp clients.
        /// </summary>
        /// <param name="actorTypeName">Actor type name to invokde the method for.</param>
        /// <param name="actorId">Actor id for the actor for which method will be invoked.</param>
        /// <param name="actorMethodName">MEthos name on actor type which will be invoked.</param>
        /// <param name="actionsActorheader">Actor Header.</param>
        /// <param name="data">Payload for the actor method.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        internal static Task<string> DispatchAsync(string actorTypeName, string actorId, string actorMethodName, string actionsActorheader, Stream data, CancellationToken cancellationToken = default(CancellationToken))
        {
            return GetActorManager(actorTypeName).DispatchAsync<string>(new ActorId(actorId), actorMethodName, actionsActorheader, data, cancellationToken);
        }

        /// <summary>
        /// Invokes the specified method for the actor when used for cross language invocation.
        /// </summary>
        /// <param name="actorTypeName">Actor type name to invokde the method for.</param>
        /// <param name="actorId">Actor id for the actor for which method will be invoked.</param>
        /// <param name="actorMethodName">MEthos name on actor type which will be invoked.</param>
        /// <param name="data">Payload for the actor method.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        internal static Task<string> DispatchForXLangInvocationAsync(string actorTypeName, string actorId, string actorMethodName, Stream data, CancellationToken cancellationToken = default(CancellationToken))
        {
            return GetActorManager(actorTypeName).DispatchForXLangInvocationAsync<string>(new ActorId(actorId), actorMethodName, data, cancellationToken);
        }

        /// <summary>
        /// Fires a reminder for the Actor.
        /// </summary>
        /// <param name="actorTypeName">Actor type name to invokde the method for.</param>
        /// <param name="actorId">Actor id for the actor for which method will be invoked.</param>
        /// <param name="reminderName">The name of reminder provided during registration.</param>
        /// <param name="state">The user state provided during registration.</param>
        /// <param name="dueTime">The invocation due time provided during registration.</param>
        /// <param name="period">The invocation period provided during registration.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        internal static Task FireReminderAsync(string actorTypeName, string actorId, string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period, CancellationToken cancellationToken = default(CancellationToken))
        {
            return GetActorManager(actorTypeName).FireReminderAsync(new ActorId(actorId), reminderName, state, dueTime, period, cancellationToken);
        }

        private static ActorTypeInfo GetActorTypeInfo(string actorTypeName)
        {
            return actorManagers[actorTypeName].ActorTypeInfo;
        }

        private static ActorManager GetActorManager(string actorTypeName)
        {
            if (!actorManagers.TryGetValue(actorTypeName, out var actorManager))
            {                
                var errorMsg = $"Actor type {actorTypeName} is not registerd with Actor runtime.";
                ActorTrace.Instance.WriteError(TraceType, errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            return actorManager;
        }
    }
}
