// ------------------------------------------------------------------------
// Copyright 2022 The Dapr Authors
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

using System.Collections.Generic;
/// <summary>
/// This class defines configurations for the subscribe endpoint.
/// </summary>
public class TopicOptions
{
    /// <summary>
    /// Gets or Sets the topic name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or Sets the name of the pubsub component to use.
    /// </summary>
    public string PubsubName { get; set; }

    /// <summary>
    /// Gets or Sets a value which indicates whether to enable or disable processing raw messages.
    /// </summary>
    public bool EnableRawPayload { get; set; }

    /// <summary>
    /// Gets or Sets the CEL expression to use to match events for this handler.
    /// </summary>
    public string Match { get; set; }

    /// <summary>
    /// Gets or Sets the priority in which this rule should be evaluated (lower to higher).
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or Sets the <see cref="IOriginalTopicMetadata.Id"/> owned by topic.
    /// </summary>
    public string[] OwnedMetadatas { get; set; }

    /// <summary>
    /// Get or Sets the separator to use for metadata.
    /// </summary>
    public string MetadataSeparator { get; set; }

    /// <summary>
    /// Gets or Sets the dead letter topic.
    /// </summary>
    public string DeadLetterTopic { get; set; }

    /// <summary>
    /// Gets or Sets the original topic metadata.
    /// </summary>
    public IDictionary<string, string> Metadata;
}