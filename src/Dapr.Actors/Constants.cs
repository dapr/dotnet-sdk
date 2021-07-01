// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors
{
    /// <summary>
    /// Contains Constant string values used by the library.
    /// </summary>
    internal static class Constants
    {
        public const string RequestIdHeaderName = "X-DaprRequestId";
        public const string RequestHeaderName = "X-DaprRequestHeader";
        public const string ErrorResponseHeaderName = "X-DaprErrorResponseHeader";
        public const string ReentrancyRequestHeaderName = "Dapr-Reentrancy-Id";
        public const string Dapr = "dapr";
        public const string Config = "config";
        public const string State = "state";
        public const string Actors = "actors";
        public const string Namespace = "urn:actors";
        public const string DaprDefaultEndpoint = "127.0.0.1";
        public const string DaprDefaultPort = "3500";
        public const string DaprVersion = "v1.0";
        public const string Method = "method";
        public const string Reminders = "reminders";
        public const string Timers = "timers";

        /// <summary>
        /// Gets string format for Actors state management relative url.
        /// </summary>
        public static string ActorStateKeyRelativeUrlFormat => $"{DaprVersion}/{Actors}/{{0}}/{{1}}/{State}/{{2}}";

        /// <summary>
        /// Gets string format for Actors state management relative url.
        /// </summary>
        public static string ActorStateRelativeUrlFormat => $"{DaprVersion}/{Actors}/{{0}}/{{1}}/{State}";

        /// <summary>
        /// Gets string format for Actors method invocation relative url.
        /// </summary>
        public static string ActorMethodRelativeUrlFormat => $"{DaprVersion}/{Actors}/{{0}}/{{1}}/{Method}/{{2}}";

        /// <summary>
        /// Gets string format for Actors reminder registration relative url..
        /// </summary>
        public static string ActorReminderRelativeUrlFormat => $"{DaprVersion}/{Actors}/{{0}}/{{1}}/{Reminders}/{{2}}";

        /// <summary>
        /// Gets string format for Actors timer registration relative url..
        /// </summary>
        public static string ActorTimerRelativeUrlFormat => $"{DaprVersion}/{Actors}/{{0}}/{{1}}/{Timers}/{{2}}";

        /// <summary>
        /// Error code indicating there was a failure invoking an actor method.
        /// </summary>
        public static string ErrorActorInvokeMethod = "ERR_ACTOR_INVOKE_METHOD";

        /// <summary>
        /// Error code indicating something does not exist.
        /// </summary>
        public static string ErrorDoesNotExist = "ERR_DOES_NOT_EXIST";

        /// <summary>
        /// Error code indicating an unknonwn/unspecified error.
        /// </summary>
        public static string Unknown = "UNKNOWN";
    }
}
