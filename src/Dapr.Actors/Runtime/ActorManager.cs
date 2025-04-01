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

namespace Dapr.Actors.Runtime;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Communication;
using Microsoft.Extensions.Logging;

// The ActorManager serves as a cache for a variety of different concerns related to an Actor type
// as well as the runtime managment for Actor instances of that type.
internal sealed class ActorManager
{
    private const string ReceiveReminderMethodName = "ReceiveReminderAsync";
    private const string TimerMethodName = "FireTimerAsync";
    private readonly ActorRegistration registration;
    private readonly ActorActivator activator;
    private readonly ILoggerFactory loggerFactory;
    private readonly IActorProxyFactory proxyFactory;
    private readonly ActorTimerManager timerManager;
    private readonly ConcurrentDictionary<ActorId, ActorActivatorState> activeActors;
    private readonly ActorMethodContext reminderMethodContext;
    private readonly ActorMethodContext timerMethodContext;
    private readonly ActorMessageSerializersManager serializersManager;
    private readonly IActorMessageBodyFactory messageBodyFactory;
    private readonly JsonSerializerOptions jsonSerializerOptions;

    // method dispatchermap used by remoting calls.
    private readonly ActorMethodDispatcherMap methodDispatcherMap;

    // method info map used by non-remoting calls.
    private readonly ActorMethodInfoMap actorMethodInfoMap;

    private readonly ILogger logger;
    private IDaprInteractor daprInteractor { get; }


    internal ActorManager(
        ActorRegistration registration,
        ActorActivator activator, 
        JsonSerializerOptions jsonSerializerOptions,
        bool useJsonSerialization,
        ILoggerFactory loggerFactory,
        IActorProxyFactory proxyFactory,
        IDaprInteractor daprInteractor)
    {
        this.registration = registration;
        this.activator = activator;
        this.jsonSerializerOptions = jsonSerializerOptions;
        this.loggerFactory = loggerFactory;
        this.proxyFactory = proxyFactory;
        this.daprInteractor = daprInteractor;

        this.timerManager = new DefaultActorTimerManager(this.daprInteractor);

        // map for remoting calls.
        this.methodDispatcherMap = new ActorMethodDispatcherMap(this.registration.Type.InterfaceTypes);

        // map for non-remoting calls.
        this.actorMethodInfoMap = new ActorMethodInfoMap(this.registration.Type.InterfaceTypes);
        this.activeActors = new ConcurrentDictionary<ActorId, ActorActivatorState>();
        this.reminderMethodContext = ActorMethodContext.CreateForReminder(ReceiveReminderMethodName);
        this.timerMethodContext = ActorMethodContext.CreateForTimer(TimerMethodName);

        // provide a serializer if 'useJsonSerialization' is true and no serialization provider is provided.
        IActorMessageBodySerializationProvider serializationProvider = null;
        if (useJsonSerialization)
        {
            serializationProvider = new ActorMessageBodyJsonSerializationProvider(jsonSerializerOptions);
        }

        this.serializersManager = IntializeSerializationManager(serializationProvider);
        this.messageBodyFactory = new WrappedRequestMessageFactory();

        this.logger = loggerFactory.CreateLogger(this.GetType());
    }

    internal ActorTypeInformation ActorTypeInfo => this.registration.Type;

    internal async Task<Tuple<string, byte[]>> DispatchWithRemotingAsync(ActorId actorId, string actorMethodName, string daprActorheader, Stream data, CancellationToken cancellationToken)
    {
        var actorMethodContext = ActorMethodContext.CreateForActor(actorMethodName);

        // Get the serialized header
        var actorMessageHeader = this.serializersManager.GetHeaderSerializer()
            .DeserializeRequestHeaders(new MemoryStream(Encoding.ASCII.GetBytes(daprActorheader)));

        var interfaceId = actorMessageHeader.InterfaceId;

        // Get the deserialized Body.
        var msgBodySerializer = this.serializersManager.GetRequestMessageBodySerializer(actorMessageHeader.InterfaceId, actorMethodContext.MethodName);
            
        IActorRequestMessageBody actorMessageBody;
        using (var stream = new MemoryStream())
        {
            await data.CopyToAsync(stream);
            actorMessageBody = await msgBodySerializer.DeserializeAsync(stream);
        }

        // Call the method on the method dispatcher using the Func below.
        var methodDispatcher = this.methodDispatcherMap.GetDispatcher(actorMessageHeader.InterfaceId);

        // Create a Func to be invoked by common method.
        async Task<Tuple<string, byte[]>> RequestFunc(Actor actor, CancellationToken ct)
        {
            IActorResponseMessageBody responseMsgBody = null;

            responseMsgBody = (IActorResponseMessageBody)await methodDispatcher.DispatchAsync(
                actor,
                actorMessageHeader.MethodId,
                actorMessageBody,
                this.messageBodyFactory,
                ct);

            return this.CreateResponseMessage(responseMsgBody, interfaceId, actorMethodContext.MethodName);
        }

        return await this.DispatchInternalAsync(actorId, actorMethodContext, RequestFunc, cancellationToken);
    }

    internal async Task DispatchWithoutRemotingAsync(ActorId actorId, string actorMethodName, Stream requestBodyStream, Stream responseBodyStream, CancellationToken cancellationToken)
    {
        var actorMethodContext = ActorMethodContext.CreateForActor(actorMethodName);

        // Create a Func to be invoked by common method.
        var methodInfo = this.actorMethodInfoMap.LookupActorMethodInfo(actorMethodName);

        async Task<object> RequestFunc(Actor actor, CancellationToken ct)
        {
            var parameters = methodInfo.GetParameters();
            dynamic awaitable;

            if (parameters.Length == 0 || (parameters.Length == 1 && parameters[0].ParameterType == typeof(CancellationToken)))
            {
                awaitable = methodInfo.Invoke(actor, parameters.Length == 0 ? null : new object[] { ct });
            }
            else if (parameters.Length == 1 || (parameters.Length == 2 && parameters[1].ParameterType == typeof(CancellationToken)))
            {
                // deserialize using stream.
                var type = parameters[0].ParameterType;
                var deserializedType = await JsonSerializer.DeserializeAsync(requestBodyStream, type, jsonSerializerOptions);
                awaitable = methodInfo.Invoke(actor, parameters.Length == 1 ? new object[] { deserializedType } : new object[] { deserializedType, ct });
            }
            else
            {
                var errorMsg = $"Method {string.Concat(methodInfo.DeclaringType.Name, ".", methodInfo.Name)} has more than one parameter and can't be invoked through http";
                throw new ArgumentException(errorMsg);
            }

            await awaitable;

            // Handle the return type of method correctly.
            if (methodInfo.ReturnType.Name != typeof(Task).Name)
            {
                // already await, Getting result will be non blocking.
                var x = awaitable.GetAwaiter().GetResult();
                return x;
            }
            else
            {
                return default;
            }
        }

        var result = await this.DispatchInternalAsync(actorId, actorMethodContext, RequestFunc, cancellationToken);

        // Write Response back if method's return type is other than Task.
        // Serialize result if it has result (return type was not just Task.)
        if (methodInfo.ReturnType.Name != typeof(Task).Name)
        {
#if NET7_0_OR_GREATER
            var resultType = methodInfo.ReturnType.GenericTypeArguments[0];
            await JsonSerializer.SerializeAsync(responseBodyStream, result, resultType, jsonSerializerOptions);
#else
                await JsonSerializer.SerializeAsync<object>(responseBodyStream, result, jsonSerializerOptions); 
#endif

        }
    }

    internal async Task FireReminderAsync(ActorId actorId, string reminderName, Stream requestBodyStream, CancellationToken cancellationToken = default)
    {
        // Only FireReminder if its IRemindable, else ignore it.
        if (this.ActorTypeInfo.IsRemindable)
        {
            var reminderdata = await ReminderInfo.DeserializeAsync(requestBodyStream);

            // Create a Func to be invoked by common method.
            async Task<byte[]> RequestFunc(Actor actor, CancellationToken ct)
            {
                await
                    (actor as IRemindable).ReceiveReminderAsync(
                        reminderName,
                        reminderdata.Data,
                        reminderdata.DueTime,
                        reminderdata.Period);

                return null;
            }

            await this.DispatchInternalAsync(actorId, this.reminderMethodContext, RequestFunc, cancellationToken);
        }
    }

    internal async Task FireTimerAsync(ActorId actorId, Stream requestBodyStream, CancellationToken cancellationToken = default)
    {
#pragma warning disable 0618
        var timerData = await JsonSerializer.DeserializeAsync<TimerInfo>(requestBodyStream);
#pragma warning restore 0618

        // Create a Func to be invoked by common method.
        async Task<byte[]> RequestFunc(Actor actor, CancellationToken ct)
        {
            var actorType = actor.Host.ActorTypeInfo.ImplementationType;
            var methodInfo = actor.GetMethodInfoUsingReflection(actorType, timerData.Callback);

            var parameters = methodInfo.GetParameters();

            // The timer callback routine needs to return a type Task
            await (Task)(methodInfo.Invoke(actor, (parameters.Length == 0) ? null : new object[] { timerData.Data }));

            return default;
        }

        await this.DispatchInternalAsync(actorId, this.timerMethodContext, RequestFunc, cancellationToken);
    }

    internal async Task ActivateActorAsync(ActorId actorId)
    {
        // An actor is activated by "Dapr" runtime when a call is to be made for an actor.
        var state = await this.CreateActorAsync(actorId);

        try
        {
            await state.Actor.OnActivateInternalAsync();
        }
        catch
        {
            // Ensure we don't leak resources if user-code throws during activation.
            await DeleteActorAsync(state);
            throw;
        }

        // Add actor to activeActors only after OnActivate succeeds (user code can throw error from its override of Activate method.)
        //
        // In theory the Dapr runtime protects us from double-activation - there's no case
        // where we *expect* to see the *update* code path taken. However it's a possiblity and
        // we should handle it.
        //
        // The policy we have chosen is to always keep the registered instance if we hit a double-activation
        // so that means we have to destroy the 'new' instance.
        var current = this.activeActors.AddOrUpdate(actorId, state, (key, oldValue) => oldValue);
        if (object.ReferenceEquals(state, current))
        {
            // On this code path it was an *Add*. Nothing left to do.
            return;
        }

        // On this code path it was an *Update*. We need to destroy the new instance and clean up.
        await DeactivateActorCore(state);
    }

    private async Task<ActorActivatorState> CreateActorAsync(ActorId actorId)
    {
        this.logger.LogDebug("Creating Actor of type {ActorType} with ActorId {ActorId}", this.ActorTypeInfo.ImplementationType, actorId);
        var host = new ActorHost(this.ActorTypeInfo, actorId, this.jsonSerializerOptions, this.loggerFactory, this.proxyFactory, this.timerManager)
        {
            StateProvider = new DaprStateProvider(this.daprInteractor, this.jsonSerializerOptions),
        };
        var state =  await this.activator.CreateAsync(host);
        this.logger.LogDebug("Finished creating Actor of type {ActorType} with ActorId {ActorId}", this.ActorTypeInfo.ImplementationType, actorId);
        return state;
    }

    internal async Task DeactivateActorAsync(ActorId actorId)
    {
        if (this.activeActors.TryRemove(actorId, out var deactivatedActor))
        {
            await DeactivateActorCore(deactivatedActor);
        }
    }

    private async Task DeactivateActorCore(ActorActivatorState state)
    {
        try
        {
            await state.Actor.OnDeactivateInternalAsync();
        }
        finally
        {
            // Ensure we don't leak resources if user-code throws during deactivation.
            await DeleteActorAsync(state);
        }
    }

    private async Task DeleteActorAsync(ActorActivatorState state)
    {
        this.logger.LogDebug("Deleting Actor of type {ActorType} with ActorId {ActorId}", this.ActorTypeInfo.ImplementationType, state.Actor.Id);
        await this.activator.DeleteAsync(state);
        this.logger.LogDebug("Finished deleting Actor of type {ActorType} with ActorId {ActorId}", this.ActorTypeInfo.ImplementationType, state.Actor.Id);
    }

    // Used for testing - do not leak the actor instances outside of this method in library code.
    public bool TryGetActorAsync(ActorId id, out Actor actor)
    {
        var found = this.activeActors.TryGetValue(id, out var state);
        actor = found ? state.Actor : default;
        return found;
    } 

    private static ActorMessageSerializersManager IntializeSerializationManager(
        IActorMessageBodySerializationProvider serializationProvider)
    {
        // TODO serializer settings
        return new ActorMessageSerializersManager(
            serializationProvider,
            new ActorMessageHeaderSerializer());
    }

    private async Task<T> DispatchInternalAsync<T>(ActorId actorId, ActorMethodContext actorMethodContext, Func<Actor, CancellationToken, Task<T>> actorFunc, CancellationToken cancellationToken)
    {
        if (!this.activeActors.ContainsKey(actorId))
        {
            await this.ActivateActorAsync(actorId);
        }

        if (!this.activeActors.TryGetValue(actorId, out var state))
        {             
            var errorMsg = $"Actor {actorId} is not yet activated.";
            throw new InvalidOperationException(errorMsg);
        }

        var actor = state.Actor;

        T retval;
        try
        {
            // Set the state context of the request, if required and possible.
            if (ActorReentrancyContextAccessor.ReentrancyContext != null)
            {
                if (state.Actor.StateManager is IActorContextualState contextualStateManager)
                {
                    await contextualStateManager.SetStateContext(Guid.NewGuid().ToString());
                }
            }

            // invoke the function of the actor
            await actor.OnPreActorMethodAsyncInternal(actorMethodContext);
            retval = await actorFunc.Invoke(actor, cancellationToken);

            // PostActivate will save the state, its not invoked when actorFunc invocation throws.
            await actor.OnPostActorMethodAsyncInternal(actorMethodContext);
        }
        catch (Exception e)
        {
            await actor.OnActorMethodFailedInternalAsync(actorMethodContext, e);
            throw;
        }
        finally
        {
            // Set the state context of the request, if possible.
            if (state.Actor.StateManager is IActorContextualState contextualStateManager)
            {
                await contextualStateManager.SetStateContext(null);
            }
        }

        return retval;
    }

    private Tuple<string, byte[]> CreateResponseMessage(IActorResponseMessageBody msgBody, int interfaceId, string methodName)
    {
        var responseMsgBodyBytes = Array.Empty<byte>();
        if (msgBody != null)
        {
            var responseSerializer = this.serializersManager.GetResponseMessageBodySerializer(interfaceId, methodName);
            responseMsgBodyBytes = responseSerializer.Serialize(msgBody);
        }

        return new Tuple<string, byte[]>(string.Empty, responseMsgBodyBytes);
    }
}