// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

namespace Dapr;

/// <summary>
/// ITopicMetadata that describes an endpoint as a subscriber to a topic.
/// </summary>
public interface ITopicMetadata
{
    /// <summary>
    /// Gets the topic name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the pubsub component name name.
    /// </summary>
    string PubsubName { get; }

    /// <summary>
    /// The CEL expression to use to match events for this handler.
    /// </summary>
    string Match { get; }

    /// <summary>
    /// The priority in which this rule should be evaluated (lower to higher).
    /// </summary>
    int Priority { get; }
}