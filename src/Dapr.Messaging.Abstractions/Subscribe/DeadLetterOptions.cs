// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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

#nullable disable
namespace Dapr.Messaging;

/// <summary>
/// Options describing the dead-letter topic for a subscription.
/// </summary>
public sealed class DeadLetterOptions
{
    /// <summary>
    /// The name of the dead-letter topic to send unprocessed messages to.
    /// </summary>
    public string TopicName { get; init; }
}
