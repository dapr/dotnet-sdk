// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//  ------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

namespace Dapr.TestContainers.Containers.Components;

/// <summary>
/// Represents a Dapr component.
/// </summary>
/// <param name="name">The name of the component.</param>
/// <param name="type">The type of the component.</param>
/// <param name="version">The component's version.</param>
/// <param name="metadata">Metadata associated with the component.</param>
public abstract class Component(string name, string type, string version, Dictionary<string, string> metadata)
{
    /// <summary>
    /// The name of the component.
    /// </summary>
	public string Name => name;
    /// <summary>
    /// The type of the component.
    /// </summary>
	public string Type => type;
    /// <summary>
    /// The version of the component.
    /// </summary>
	public string Version => version;
    /// <summary>
    /// The metadata attached to the component.
    /// </summary>
	public List<MetadataEntry> Metadata => metadata.Select(kv => new MetadataEntry(kv.Key, kv.Value)).ToList();
}
