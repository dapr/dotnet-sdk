﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors
{
    /// <summary>
    /// Definitions for Dapr Error Codes.
    /// </summary>
    public enum DaprErrorCodes
    {
        /// <summary>
        /// Unknown Error Code.
        /// </summary>
        UNKNOWN = 0,

        /// <summary>
        /// Error requesting a resource/content/path that does not exist.
        /// </summary>
        ERR_DOES_NOT_EXIST,

        /// <summary>
        /// Error referencing an actor instance not found.
        /// </summary>
        ERR_ACTOR_INSTANCE_MISSING,

        /// <summary>
        /// Error referencing an actor runtime not found.
        /// </summary>
        ERR_ACTOR_RUNTIME_NOT_FOUND,

        /// <summary>
        /// Error creating a reminder for an actor.
        /// </summary>
        ERR_ACTOR_REMINDER_CREATE,

        /// <summary>
        /// Error deleting a reminder for an actor.
        /// </summary>
        ERR_ACTOR_REMINDER_DELETE,

        /// <summary>
        /// Error creating a timer for an actor.
        /// </summary>
        ERR_ACTOR_TIMER_CREATE,

        /// <summary>
        /// Error deleting a timer for an actor.
        /// </summary>
        ERR_ACTOR_TIMER_DELETE,

        /// <summary>
        /// Error getting a reminder for an actor.
        /// </summary>
        ERR_ACTOR_REMINDER_GET,

        /// <summary>
        /// Error invoking a method on an actor.
        /// </summary>
        ERR_ACTOR_INVOKE_METHOD,

        /// <summary>
        /// Error deleting the state for an actor.
        /// </summary>
        ERR_ACTOR_STATE_DELETE,

        /// <summary>
        /// Error getting the state for an actor.
        /// </summary>
        ERR_ACTOR_STATE_GET,

        /// <summary>
        /// Error saving the state for an actor.
        /// </summary>
        ERR_ACTOR_STATE_SAVE,

        /// <summary>
        /// Error storing actor state transactionally.
        /// </summary>
        ERR_ACTOR_STATE_TRANSACTION_SAVE,

        /// <summary>
        /// Error referencing a Pub/Sub not found.
        /// </summary>
        ERR_PUBSUB_NOT_FOUND,

        /// <summary>
        /// Error publishing a message.
        /// </summary>
        ERR_PUBSUB_PUBLISH_MESSAGE,

        /// <summary>
        /// Error serializing Pub/Sub event envelope.
        /// </summary>
        ERR_PUBSUB_CLOUD_EVENTS_SER,

        /// <summary>
        /// Error referencing a state store not found.
        /// </summary>
        ERR_STATE_STORE_NOT_FOUND,

        /// <summary>
        /// Error getting a state store.
        /// </summary>
        ERR_STATE_GET,

        /// <summary>
        /// Error deleting a state store.
        /// </summary>
        ERR_STATE_DELETE,

        /// <summary>
        /// Error saving state store.
        /// </summary>
        ERR_STATE_SAVE,

        /// <summary>
        /// Error referencing a state store with specified key not found.
        /// </summary>
        ERR_STATE_KEY_NOT_FOUND,

        /// <summary>
        /// Error invoking an output binding.
        /// </summary>
        ERR_INVOKE_OUTPUT_BINDING,

        /// <summary>
        /// Error with a malformed request.
        /// </summary>
        ERR_MALFORMED_REQUEST,

        /// <summary>
        /// Error in direct invocation.
        /// </summary>
        ERR_DIRECT_INVOKE,

        /// <summary>
        /// Error deserializing an HTTP request body.
        /// </summary>
        ERR_DESERIALIZE_HTTP_BODY,
    }
}
