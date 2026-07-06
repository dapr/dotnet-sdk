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

using System;

/// <summary>
/// TopicAttribute describes an endpoint as a subscriber to a topic.
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
public class TopicAttribute : Attribute, ITopicMetadata, IRawTopicMetadata, IOwnedOriginalTopicMetadata, IDeadLetterTopicMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TopicAttribute" /> class.
    /// </summary>
    /// <param name="pubsubName">The name of the pubsub component to use.</param>
    /// <param name="name">The topic name.</param>
    /// <param name="ownedMetadatas">The topic owned metadata ids.</param>
    /// <param name="metadataSeparator">Separator to use for metadata.</param>
    public TopicAttribute(string pubsubName, string name, string[] ownedMetadatas = null, string metadataSeparator = null)
    {
        ArgumentVerifier.ThrowIfNullOrEmpty(pubsubName, nameof(pubsubName));
        ArgumentVerifier.ThrowIfNullOrEmpty(name, nameof(name));

        this.Name = name;
        this.PubsubName = pubsubName;
        this.OwnedMetadatas = ownedMetadatas;
        this.MetadataSeparator = metadataSeparator;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TopicAttribute" /> class.
    /// </summary>
    /// <param name="pubsubName">The name of the pubsub component to use.</param>
    /// <param name="name">The topic name.</param>
    /// <param name="enableRawPayload">The enable/disable raw pay load flag.</param>
    /// <param name="ownedMetadatas">The topic owned metadata ids.</param>
    /// <param name="metadataSeparator">Separator to use for metadata.</param>
    public TopicAttribute(string pubsubName, string name, bool enableRawPayload, string[] ownedMetadatas = null, string metadataSeparator = null)
    {
        ArgumentVerifier.ThrowIfNullOrEmpty(pubsubName, nameof(pubsubName));
        ArgumentVerifier.ThrowIfNullOrEmpty(name, nameof(name));

        this.Name = name;
        this.PubsubName = pubsubName;
        this.EnableRawPayload = enableRawPayload;
        this.OwnedMetadatas = ownedMetadatas;
        this.MetadataSeparator = metadataSeparator;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TopicAttribute" /> class.
    /// </summary>
    /// <param name="pubsubName">The name of the pubsub component to use.</param>
    /// <param name="name">The topic name.</param>
    /// <param name="match">The CEL expression to test the cloud event with.</param>
    /// <param name="priority">The priority of the rule (low-to-high values).</param>
    /// <param name="ownedMetadatas">The topic owned metadata ids.</param>
    /// <param name="metadataSeparator">Separator to use for metadata.</param>
    public TopicAttribute(string pubsubName, string name, string match, int priority, string[] ownedMetadatas = null, string metadataSeparator = null)
    {
        ArgumentVerifier.ThrowIfNullOrEmpty(pubsubName, nameof(pubsubName));
        ArgumentVerifier.ThrowIfNullOrEmpty(name, nameof(name));

        this.Name = name;
        this.PubsubName = pubsubName;
        this.Match = match;
        this.Priority = priority;
        this.OwnedMetadatas = ownedMetadatas;
        this.MetadataSeparator = metadataSeparator;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TopicAttribute" /> class.
    /// </summary>
    /// <param name="pubsubName">The name of the pubsub component to use.</param>
    /// <param name="name">The topic name.</param>
    /// <param name="enableRawPayload">The enable/disable raw pay load flag.</param>
    /// <param name="match">The CEL expression to test the cloud event with.</param>
    /// <param name="priority">The priority of the rule (low-to-high values).</param>
    /// <param name="ownedMetadatas">The topic owned metadata ids.</param>
    /// <param name="metadataSeparator">Separator to use for metadata.</param>
    public TopicAttribute(string pubsubName, string name, bool enableRawPayload, string match, int priority, string[] ownedMetadatas = null, string metadataSeparator = null)
    {
        ArgumentVerifier.ThrowIfNullOrEmpty(pubsubName, nameof(pubsubName));
        ArgumentVerifier.ThrowIfNullOrEmpty(name, nameof(name));

        this.Name = name;
        this.PubsubName = pubsubName;
        this.EnableRawPayload = enableRawPayload;
        this.Match = match;
        this.Priority = priority;
        this.OwnedMetadatas = ownedMetadatas;
        this.MetadataSeparator = metadataSeparator;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TopicAttribute" /> class.
    /// </summary>
    /// <param name="pubsubName">The name of the pubsub component to use.</param>
    /// <param name="name">The topic name.</param>
    /// <param name="deadLetterTopic">The dead letter topic name.</param>
    /// <param name="enableRawPayload">The enable/disable raw pay load flag.</param>
    /// <param name="ownedMetadatas">The topic owned metadata ids.</param>
    /// <param name="metadataSeparator">Separator to use for metadata.</param>
    public TopicAttribute(string pubsubName, string name, string deadLetterTopic, bool enableRawPayload, string[] ownedMetadatas = null, string metadataSeparator = null)
    {
        ArgumentVerifier.ThrowIfNullOrEmpty(pubsubName, nameof(pubsubName));
        ArgumentVerifier.ThrowIfNullOrEmpty(name, nameof(name));

        this.Name = name;
        this.PubsubName = pubsubName;
        this.DeadLetterTopic = deadLetterTopic;
        this.EnableRawPayload = enableRawPayload;
        this.OwnedMetadatas = ownedMetadatas;
        this.MetadataSeparator = metadataSeparator;
    }

    /// <inheritdoc/>
    public string Name { get; set; }

    /// <inheritdoc/>
    public string PubsubName { get; set; }

    /// <inheritdoc/>
    public bool? EnableRawPayload { get; set; }

    /// <inheritdoc/>
    public new string Match { get; set; }

    /// <inheritdoc/>
    public int Priority { get; set; }

    /// <inheritdoc/>
    public string[] OwnedMetadatas { get; set; }

    /// <inheritdoc/>
    public string MetadataSeparator { get; set; }

    /// <inheritdoc/>
    public string DeadLetterTopic { get; set; }
}