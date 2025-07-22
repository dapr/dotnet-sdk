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
/// IOwnedOriginalTopicMetadata that describes subscribe endpoint to topic owned metadata.
/// </summary>
public interface IOwnedOriginalTopicMetadata
{
    /// <summary>
    /// Gets the <see cref="IOriginalTopicMetadata.Id"/> owned by topic.
    /// </summary>
    /// <remarks>When the <see cref="IOriginalTopicMetadata.Id"/> is not empty, the metadata owned by topic.</remarks>
    string[] OwnedMetadatas { get; }

    /// <summary>
    ///  Get separator to use for metadata
    /// </summary>
    /// <remarks>Separator to be used for <see cref="IOriginalTopicMetadata.Value"/> when multiple values exist for a <see cref="IOriginalTopicMetadata.Name"/>.</remarks>
    string MetadataSeparator { get; }
}