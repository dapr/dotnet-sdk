// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors
{
    /// <summary>
    /// Defines values for DaprErrorCodes.
    /// </summary>
    public enum DaprErrorCodes
    {
        /// <summary>
        /// Unknown Error Code.
        /// </summary>
        UNKNOWN = 0,

        /// <summary>
        /// Requested resource/content/path does not exist.
        /// </summary>
        ERR_DOES_NOT_EXIST,

        /// <summary>
        /// Error invoking output binding.
        /// </summary>
        ERR_INVOKE_OUTPUT_BINDING,

        /// <summary>
        /// State store is not found.
        /// </summary>
        ERR_STATE_STORE_NOT_FOUND,

        /// <summary>
        /// Error in getting state.
        /// </summary>
        ERR_GET_STATE,

        /// <summary>
        /// Error in deleting state.
        /// </summary>
        ERR_DELETE_STATE,

        /// <summary>
        /// Malformed request.
        /// </summary>
        ERR_MALFORMED_REQUEST,

        /// <summary>
        /// Error in saving state.
        /// </summary>
        ERR_SAVE_REQUEST,

        /// <summary>
        /// Error in direct invocation.
        /// </summary>
        ERR_DIRECT_INVOKE,

        /// <summary>
        /// Error in invocation.
        /// </summary>
        ERR_INVOKE,

        /// <summary>
        /// Error when actor runtime is not found.
        /// </summary>
        ERR_ACTOR_RUNTIME_NOT_FOUND,

        /// <summary>
        /// Error in creating reminder for the actor.
        /// </summary>
        ERR_CREATE_REMINDER,

        /// <summary>
        /// Error in creating timer for the actor.
        /// </summary>
        ERR_CREATE_TIMER,

        /// <summary>
        /// Error in deleting reminder for the actor.
        /// </summary>
        ERR_DELETE_REMINDER,

        /// <summary>
        /// Error while storing actor state transactionally.
        /// </summary>
        ERR_ACTOR_STATE_TRANSACTION,

        /// <summary>
        /// Error in deleting timer for the actor.
        /// </summary>
        ERR_DELETE_TIMER,

        /// <summary>
        /// Error in invoking actor method.
        /// </summary>
        ERR_INVOKE_ACTOR,

        /// <summary>
        /// Error in deserializing http request body.
        /// </summary>
        ERR_DESERIALIZE_HTTP_BODY,

        /// <summary>
        /// Error in getting state for the actor.
        /// </summary>
        ERR_ACTOR_GET_STATE,

        /// <summary>
        /// Error in deleting state for the actor.
        /// </summary>
        ERR_ACTOR_DELETE_STATE,

        /// <summary>
        /// Pub sub not found.
        /// </summary>
        ERR_PUB_SUB_NOT_FOUND,

        /// <summary>
        /// Error in publishig message.
        /// </summary>
        ERR_PUBLISH_MESSAGE,

        /// <summary>
        /// Actor not found.
        /// </summary>
        ERR_ACTOR_INSTANCE_MISSING,
    }
}
