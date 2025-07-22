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

using System.Collections.Generic;

namespace Dapr.Client;

/// <summary>
/// Represents a metadata object returned from dapr sidecar.
/// </summary>
/// <param name="id">The application id.</param>
/// <param name="actors">The registered actors metadata.</param>
/// <param name="extended">The list of custom attributes as key-value pairs, where key is the attribute name.</param>
/// <param name="components">The loaded  components metadata.</param>
public sealed class DaprMetadata(string id, IReadOnlyList<DaprActorMetadata> actors, IReadOnlyDictionary<string, string> extended, IReadOnlyList<DaprComponentsMetadata> components)
{
    /// <summary>
    /// Gets the application id.
    /// </summary>
    public string Id { get; } = id;

    /// <summary>
    /// Gets the registered actors metadata.
    /// </summary>
    public IReadOnlyList<DaprActorMetadata> Actors { get; } = actors;

    /// <summary>
    /// Gets the list of custom attributes as key-value pairs, where key is the attribute name.
    /// </summary>
    public IReadOnlyDictionary<string, string> Extended { get; } = extended;

    /// <summary>
    ///  Gets the loaded  components metadata.
    /// </summary>
    public IReadOnlyList<DaprComponentsMetadata> Components { get; } = components;
}

/// <summary>
/// Represents a actor metadata object returned from dapr sidecar.
/// </summary>
/// <param name="type">This registered actor type.</param>
/// <param name="count">The number of actors running.</param>
public sealed class DaprActorMetadata(string type, int count)
{
    /// <summary>
    /// Gets the registered actor type.
    /// </summary>
    public string Type { get; } = type;

    /// <summary>
    /// Gets the number of actors running.
    /// </summary>
    public int Count { get; } = count;
}

/// <summary>
/// Represents a components metadata object returned from dapr sidecar.
/// </summary>
/// <param name="name">The name of the component.</param>
/// <param name="type">The component type.</param>
/// <param name="version">The component version.</param>
/// <param name="capabilities">The supported capabilities for this component type and version.</param>
public sealed class DaprComponentsMetadata(string name, string type, string version, string[] capabilities)
{
    /// <summary>
    /// Gets the name of the component.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the component type.
    /// </summary>
    public string Type { get; } = type;

    /// <summary>
    /// Gets the component version.
    /// </summary>
    public string Version { get; } = version;

    /// <summary>
    /// Gets the supported capabilities for this component type and version.
    /// </summary>
    public string[] Capabilities { get; } = capabilities;
}
