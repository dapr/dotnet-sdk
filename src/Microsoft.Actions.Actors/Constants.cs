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
        public const string ActionsPortEnvironmentVariable = "ACTIONSPORT";
        public const string State = "state";
        public const string Actors = "actors";
        public const string Namespace = "urn:actors";
        public const string ActionsDefaultEndpoint = "localhost";
        public const string ActionsDefaultPort = "3500";
        public const string ActionsVersion = "v1.0";
        public const string Method = "method";

        /// <summary>
        /// Gets string format for Actors state management relative url.
        /// </summary>
        public static string ActorStateRelativeUrlFormat => $"{ActionsVersion}/{Actors}/{{0}}/{{1}}/{State}/{{2}}";

        /// <summary>
        /// Gets string format for Actors method invocation relative url.
        /// </summary>
        public static string ActorMethodRelativeUrlFormat => $"{ActionsVersion}/{Actors}/{{0}}/{{1}}/{Method}/{{2}}";
    }
}
