// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Runtime
{    
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Actions.Actors.Communication;
    using Newtonsoft.Json;

    /// <summary>
    /// Manages Actors of a specific actor type.
    /// </summary>
    internal sealed class ActorManager : IActorManager
    {
        private const string TraceType = "ActorManager";
        private const string ReceiveReminderMethodName = "ReceiveReminderAsync";
        private readonly ConcurrentDictionary<ActorId, Actor> activeActors;
        private readonly ActorMethodContext reminderMethodContext;

        internal ActorManager(ActorTypeInfo actorTypeInfo)
        {
            this.ActorTypeInfo = actorTypeInfo;
            this.activeActors = new ConcurrentDictionary<ActorId, Actor>();
            this.reminderMethodContext = ActorMethodContext.CreateForReminder(ReceiveReminderMethodName);
        }

        internal ActorTypeInfo ActorTypeInfo { get; }

        internal Task<T> DispatchWithRemotingAsync<T>(ActorId actorId, string actorMethodName, string actionsActorheader, Stream data, CancellationToken cancellationToken)
        {
            var actorMethodContext = ActorMethodContext.CreateForActor(actorMethodName);
            var header = JsonConvert.DeserializeObject<ActorRequestMessageHeader>(actionsActorheader);

            // Get the deserialized Body.
            // Add methodDispatcher.
            // Call the method on the method dispatcher using the Func below.

            // Create a Func to be invoked by common method.
            Task<T> RequestFunc(Actor actor, CancellationToken ct)
            {
                var methodInfo = this.ActorTypeInfo.LookupActorMethodInfo(actorMethodName);
                var parameters = methodInfo.GetParameters();
                var type = parameters[0].ParameterType;
                return (Task<T>)methodInfo.Invoke(actor, new object[] { JsonConvert.DeserializeObject(string.Empty, type) });
            }

            return this.DispatchInternalAsync(actorId, actorMethodContext, RequestFunc, cancellationToken);
        }

        internal Task<string> DispatchWihtoutRemotingAsync(ActorId actorId, string actorMethodName, Stream data, CancellationToken cancellationToken)
        {
            var actorMethodContext = ActorMethodContext.CreateForActor(actorMethodName);

            // Create a Func to be invoked by common method.
            var methodInfo = this.ActorTypeInfo.LookupActorMethodInfo(actorMethodName);

            async Task<string> RequestFunc(Actor actor, CancellationToken ct)
            {                
                var parameters = methodInfo.GetParameters();

                if (parameters.Length == 0)
                {
                    // dynamic task = await (Task<dynamic>)methodInfo.Invoke(actor, null);
                    dynamic awaitable = methodInfo.Invoke(actor, null);
                    await awaitable;
                    return JsonConvert.SerializeObject(awaitable.GetAwaiter().GetResult());
                }
                else
                {
                    string json = default(string);
                    using (var reader = new StreamReader(data))
                    {
                        json = reader.ReadToEnd();
                    }

                    var type = parameters[0].ParameterType;
                    dynamic awaitable = methodInfo.Invoke(actor, new object[] { JsonConvert.DeserializeObject(json, type) });
                    await awaitable;
                    return JsonConvert.SerializeObject(awaitable.GetAwaiter().GetResult());
                }                
            }

            return this.DispatchInternalAsync(actorId, actorMethodContext, RequestFunc, cancellationToken);
        }

        internal Task FireReminderAsync(ActorId actorId, string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period, CancellationToken cancellationToken = default(CancellationToken))
        {
            // Only FireReminder if its IRemindable, else ignore it.
            if (this.ActorTypeInfo.IsRemindable)
            {
                // Create a Func to be invoked by common method.
                async Task<byte[]> RequestFunc(Actor actor, CancellationToken ct)
                {
                    await
                        (actor as IRemindable).ReceiveReminderAsync(
                            reminderName,
                            state,
                            dueTime,
                            period);

                    return null;
                }

                return this.DispatchInternalAsync(actorId, this.reminderMethodContext, RequestFunc, cancellationToken);
            }

            return Task.CompletedTask;
        }

        internal async Task ActivateActor(ActorId actorId)
        {
            // An actor is activated by "actions" runtime when a call is to be made for an actor.
            var actor = this.CreateActor(actorId);
            await actor.OnActivateInternalAsync();

            // Add actor to activeActors only after OnActivate succeeds (user code can throw error from its override of Activate method.)
            // Always add the new instance.
            this.activeActors.AddOrUpdate(actorId, actor, (key, oldValue) => actor);
        }

        internal async Task DeactivateActor(ActorId actorId)
        {
            if (this.activeActors.TryRemove(actorId, out var deactivatedActor))
            {
                await deactivatedActor.OnDeactivateInternalAsync();
            }
        }

        private async Task<T> DispatchInternalAsync<T>(ActorId actorId, ActorMethodContext actorMethodContext, Func<Actor, CancellationToken, Task<T>> actorFunc, CancellationToken cancellationToken)
        {
            if (!this.activeActors.TryGetValue(actorId, out var actor))
            {
                // This should never happen, as "Actions" runtime activates the actor first. if it ever it would mean a bug in "Actions" runtime.
                var errorMsg = $"Actor {actorId} is not yet activated.";
                ActorTrace.Instance.WriteError(TraceType, errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            var retval = default(T);

            try
            {
                // invoke the function of the actor
                await actor.OnPreActorMethodAsyncInternal(actorMethodContext);
                retval = await actorFunc.Invoke(actor, cancellationToken);
                await actor.OnPostActorMethodAsyncInternal(actorMethodContext);
            }
            catch (Exception e)
            {
                actor.OnInvokeFailed();
                Console.WriteLine(e);
                throw;
            }

            return retval;
        }

        private Actor CreateActor(ActorId actorId)
        {
            return this.ActorTypeInfo.ActorFactory.Invoke(actorId);
        }
    }
}
