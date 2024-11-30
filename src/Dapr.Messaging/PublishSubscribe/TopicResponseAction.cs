// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

namespace Dapr.Messaging.PublishSubscribe;

/// <summary>
/// Describes the various actions that can be taken on a topic message.
/// </summary>
public enum TopicResponseAction
{
    /// <summary>
    /// Indicates the message was processed successfully and should be deleted from the pub/sub topic.
    /// </summary>
    Success,
    /// <summary>
    /// Indicates a failure while processing the message and that the message should be resent for a retry.
    /// </summary>
    Retry,
    /// <summary>
    /// Indicates a failure while processing the message and that the message should be dropped or sent to the
    /// dead-letter topic if specified.
    /// </summary>
    Drop
}
