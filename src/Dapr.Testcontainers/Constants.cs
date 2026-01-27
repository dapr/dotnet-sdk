// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//  ------------------------------------------------------------------------

namespace Dapr.Testcontainers;

/// <summary>
/// Various constants used throughout the project.
/// </summary>
public static class Constants
{
    /// <summary>
    /// Various names used for the Dapr components.
    /// </summary>
    public static class DaprComponentNames
    {
        /// <summary>
        /// The name of the State Management component.
        /// </summary>
        public const string StateManagementComponentName = "statestore";
        /// <summary>
        /// The name of the PubSub component.
        /// </summary>
        public const string PubSubComponentName = "pubsub";
        /// <summary>
        /// The name of the Conversation component.
        /// </summary>
        public const string ConversationComponentName = "conversation";
        /// <summary>
        /// The name of the Cryptography component.
        /// </summary>
        public const string CryptographyComponentName = "cryptography";
        /// <summary>
        /// The name of the Distributed Lock component.
        /// </summary>
        public const string DistributedLockComponentName = "distributed-lock";
    }
}
