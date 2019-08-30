// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors
{
    /// <summary>
    /// Contains Constant string values used by the library.
    /// </summary>
    internal static class Constants
    {
        public const string RequestIdHeaderName = "X-ActionsRequestId";
        public const string RequestHeaderName = "X-ActionsRequestHeader";
        public const string ErrorResponseHeaderName = "X-ActionsErrorResponseHeader";
        public const string ActionsPortEnvironmentVariable = "ACTIONS_PORT";
        public const string Actions = "actions";
        public const string Config = "config";
        public const string State = "state";
        public const string Actors = "actors";
        public const string Namespace = "urn:actors";
        public const string ActionsDefaultEndpoint = "localhost";
        public const string ActionsDefaultPort = "3500";
        public const string ActionsVersion = "v1.0";
        public const string Method = "method";
        public const string Reminders = "reminders";
        public const string Timers = "timers";

        /// <summary>
        /// Gets string format for Actors state management relative url.
        /// </summary>
        public static string ActorStateKeyRelativeUrlFormat => $"{ActionsVersion}/{Actors}/{{0}}/{{1}}/{State}/{{2}}";

        /// <summary>
        /// Gets string format for Actors state management relative url.
        /// </summary>
        public static string ActorStateRelativeUrlFormat => $"{ActionsVersion}/{Actors}/{{0}}/{{1}}/{State}";

        /// <summary>
        /// Gets string format for Actors method invocation relative url.
        /// </summary>
        public static string ActorMethodRelativeUrlFormat => $"{ActionsVersion}/{Actors}/{{0}}/{{1}}/{Method}/{{2}}";

        /// <summary>
        /// Gets string format for Actors reminder registration relative url..
        /// </summary>
        public static string ActorReminderRelativeUrlFormat => $"{ActionsVersion}/{Actors}/{{0}}/{{1}}/{Reminders}/{{2}}";

        /// <summary>
        /// Gets string format for Actors timer registration relative url..
        /// </summary>
        public static string ActorTimerRelativeUrlFormat => $"{ActionsVersion}/{Actors}/{{0}}/{{1}}/{Timers}/{{2}}";
    }
}
