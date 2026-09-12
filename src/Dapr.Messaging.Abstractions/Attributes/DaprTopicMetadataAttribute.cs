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

namespace Dapr.Messaging;

/// <summary>
/// Attaches a metadata key/value pair to one or more topics declared with <see cref="DaprTopicAttribute"/>.
/// Apply at the class or assembly level. Correlation to specific topics is by <see cref="DaprTopicAttribute.MetadataKeys"/>;
/// when <see cref="DaprTopicAttribute.MetadataKeys"/> is <c>null</c>, the metadata applies to all topics on the class.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class, AllowMultiple = true)]
public sealed class DaprTopicMetadataAttribute : Attribute
{
    /// <summary>
    /// Creates a new <see cref="DaprTopicMetadataAttribute"/>.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    public DaprTopicMetadataAttribute(string key, string value)
    {
        this.Key = key;
        this.Value = value;
    }

    /// <summary>The metadata key.</summary>
    public string Key { get; }

    /// <summary>The metadata value.</summary>
    public string Value { get; }
}
