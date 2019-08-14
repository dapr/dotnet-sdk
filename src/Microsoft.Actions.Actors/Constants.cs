// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors
{
    /// <summary>
    /// Contains Constant string values used by the library.
    /// </summary>
    internal class Constants
    {
        /// <summary>
        /// Constant string for request id in header.
        /// </summary>
        public const string RequestIdHeaderName = "X-ActionsRequestId";

        /// <summary>
        /// Constant string for request header name in header.
        /// </summary>
        public const string RequestHeaderName = "X-ActionsRequestHeader";

        /// <summary>
        /// Constant string for Environment Variable for Actions port.
        /// </summary>
        public const string ActionsPortEnvironmentVariable = "ACTIONSPORT";

        /// <summary>
        /// Constant string for Actors state management relative url.
        /// </summary>
        public const string ActorStateManagementRelativeUrl = "actions/state";

        public const string Namespace = "urn:actors";
    }
}
