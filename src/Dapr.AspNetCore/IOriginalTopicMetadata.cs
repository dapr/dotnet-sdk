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
/// IOriginalTopicMetadata that describes subscribe endpoint to a topic original metadata.
/// </summary>
public interface IOriginalTopicMetadata
{
    /// <summary>
    /// Gets the topic metadata id.
    /// </summary>
    /// <remarks>
    /// It is only used for simple identification,<see cref="IOwnedOriginalTopicMetadata.OwnedMetadatas"/>. When it is empty, it can be used for all topics in the current context.
    /// </remarks>
    string Id { get; }

    /// <summary>
    /// Gets the topic metadata name.
    /// </summary>
    /// <remarks>Multiple identical names. only the first <see cref="Value"/> is valid.</remarks>
    string Name { get; }

    /// <summary>
    ///  Gets the topic metadata value.
    /// </summary>
    string Value { get; }
}