// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System;
    using System.Text.Json;
    using Dapr.Actors.Client;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Represents a host for an actor type within the actor runtime.
    /// </summary>
    public sealed class ActorHost
    {
        /// <summary>
        /// Creates an instance of <see cref="ActorHost" /> for unit testing an actor instance.
        /// </summary>
        /// <param name="options">The <see cref="ActorTestOptions" /> for configuring the host.</param>
        /// <typeparam name="TActor">The actor type.</typeparam>
        /// <returns>An <see cref="ActorHost" /> instance.</returns>
        public static ActorHost CreateForTest<TActor>(ActorTestOptions options = null)
            where TActor : Actor
        {
            return CreateForTest(typeof(TActor), options);
        }

        /// <summary>
        /// Creates an instance of <see cref="ActorHost" /> for unit testing an actor instance.
        /// </summary>
        /// <param name="actorType">The actor type.</param>
        /// <param name="options">The <see cref="ActorTestOptions" /> for configuring the host.</param>
        /// <returns>An <see cref="ActorHost" /> instance.</returns>
        public static ActorHost CreateForTest(Type actorType, ActorTestOptions options = null)
        {
            if (actorType == null)
            {
                throw new ArgumentNullException(nameof(actorType));
            }

            options ??= new ActorTestOptions();
            
            return new ActorHost(
                ActorTypeInformation.Get(actorType),
                options.ActorId, 
                options.JsonSerializerOptions,
                options.LoggerFactory,
                options.ProxyFactory,
                options.TimerManager);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorHost"/> class.
        /// </summary>
        /// <param name="actorTypeInfo">The type information of the Actor.</param>
        /// <param name="id">The id of the Actor instance.</param>
        /// <param name="jsonSerializerOptions">The <see cref="JsonSerializerOptions"/> to use for actor state persistence and message deserialization.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="proxyFactory">The <see cref="ActorProxyFactory" />.</param>
        [Obsolete("Application code should not call this method. Use CreateForTest for unit testing.")]
        public ActorHost(
            ActorTypeInformation actorTypeInfo,
            ActorId id,
            JsonSerializerOptions jsonSerializerOptions,
            ILoggerFactory loggerFactory,
            IActorProxyFactory proxyFactory)
        {
            this.ActorTypeInfo = actorTypeInfo;
            this.Id = id;
            this.LoggerFactory = loggerFactory;
            this.ProxyFactory = proxyFactory;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorHost"/> class.
        /// </summary>
        /// <param name="actorTypeInfo">The type information of the Actor.</param>
        /// <param name="id">The id of the Actor instance.</param>
        /// <param name="jsonSerializerOptions">The <see cref="JsonSerializerOptions"/> to use for actor state persistence and message deserialization.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="proxyFactory">The <see cref="ActorProxyFactory" />.</param>
        /// <param name="timerManager">The <see cref="ActorTimerManager" />.</param>
        internal ActorHost(
            ActorTypeInformation actorTypeInfo,
            ActorId id,
            JsonSerializerOptions jsonSerializerOptions,
            ILoggerFactory loggerFactory,
            IActorProxyFactory proxyFactory,
            ActorTimerManager timerManager)
        {
            this.ActorTypeInfo = actorTypeInfo;
            this.Id = id;
            this.LoggerFactory = loggerFactory;
            this.ProxyFactory = proxyFactory;
            this.TimerManager = timerManager;
        }

        /// <summary>
        /// Gets the ActorTypeInformation for actor service.
        /// </summary>
        public ActorTypeInformation ActorTypeInfo { get; }

        /// <summary>
        /// Gets the <see cref="ActorId" />.
        /// </summary>
        public ActorId Id { get; }

        /// <summary>
        /// Gets the LoggerFactory for actor service
        /// </summary>
        public ILoggerFactory LoggerFactory { get; }

        /// <summary>
        /// Gets the <see cref="DaprStateProvider" />.
        /// </summary>
        public JsonSerializerOptions JsonSerializerOptions { get; }

        /// <summary>
        /// Gets the <see cref="IActorProxyFactory" />.
        /// </summary>
        public IActorProxyFactory ProxyFactory { get; }

        /// <summary>
        /// Gets the <see cref="ActorTimerManager" />.
        /// </summary>
        public ActorTimerManager TimerManager { get; }

        internal DaprStateProvider StateProvider { get; set; }
    }
}
