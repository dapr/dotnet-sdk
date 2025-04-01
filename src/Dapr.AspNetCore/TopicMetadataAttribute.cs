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
/// IOriginalTopicMetadata that describes subscribe endpoint to a topic original metadata.
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
public class TopicMetadataAttribute : Attribute, IOriginalTopicMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TopicMetadataAttribute" /> class.
    /// </summary>
    /// <param name="name">The metadata name.</param>
    /// <param name="value">The metadata value.</param>
    public TopicMetadataAttribute(string name, string value)
    {
        ArgumentVerifier.ThrowIfNullOrEmpty(name, nameof(name));
        ArgumentVerifier.ThrowIfNull(value, nameof(value));
        Name = name;
        Value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TopicMetadataAttribute" /> class.
    /// </summary>
    /// <param name="id">The metadata id.</param>
    /// <param name="name">The metadata name.</param>
    /// <param name="value">The metadata value.</param>
    public TopicMetadataAttribute(string id, string name, string value)
    {
        ArgumentVerifier.ThrowIfNullOrEmpty(id, nameof(name));
        ArgumentVerifier.ThrowIfNullOrEmpty(name, nameof(name));
        ArgumentVerifier.ThrowIfNull(value, nameof(value));
        Id = id;
        Name = name;
        Value = value;
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public string Value { get; }
}