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

namespace Dapr.Metadata.Abstractions;

/// <summary>
/// Represents the metadata for a registered Dapr component.
/// </summary>
public class ComponentMetadata
{
    /// <summary>
    /// The name of the component.
    /// </summary>
    public string? Name { get; init; }
    
    /// <summary>
    /// The type of the component.
    /// </summary>
    public string? Type { get; init; }
    
    /// <summary>
    /// The version of the component.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// The supported capabilities for the component type and verison.
    /// </summary>
    public IReadOnlyCollection<string> Capabilities { get; init; } = [];
}
