// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System.Text.Json;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Represents a host for an actor type within the actor runtime.
    /// </summary>
    public sealed class ActorHost
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActorHost"/> class.
        /// </summary>
        /// <param name="actorTypeInfo">The type information of the Actor.</param>
        /// <param name="id">The id of the Actor instance.</param>
        /// <param name="jsonSerializerOptions">The <see cref="JsonSerializerOptions"/> to use for actor state persistence and message deserialization.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        public ActorHost(
            ActorTypeInformation actorTypeInfo,
            ActorId id,
            JsonSerializerOptions jsonSerializerOptions,
            ILoggerFactory loggerFactory)
        {
            this.ActorTypeInfo = actorTypeInfo;
            this.Id = id;
            this.LoggerFactory = loggerFactory;
            this.StateProvider = new DaprStateProvider(jsonSerializerOptions);
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

        internal DaprStateProvider StateProvider { get; }
    }
}
