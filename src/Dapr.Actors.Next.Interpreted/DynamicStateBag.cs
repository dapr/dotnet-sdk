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

using System.Text.Json;

namespace Dapr.Actors.Next.Interpreted;

/// <summary>
/// Mutable dynamic state payload carried by an interpreted state-machine actor.
/// </summary>
public sealed class DynamicStateBag
{
    private readonly Dictionary<string, JsonElement> values;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicStateBag"/> class.
    /// </summary>
    public DynamicStateBag()
        : this(new Dictionary<string, JsonElement>(StringComparer.Ordinal))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicStateBag"/> class.
    /// </summary>
    public DynamicStateBag(IReadOnlyDictionary<string, JsonElement> values)
    {
        this.values = new Dictionary<string, JsonElement>(values, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the stored values.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement> Values => values;

    /// <summary>
    /// Sets a JSON value.
    /// </summary>
    public void Set(string name, JsonElement value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        values[name] = value.Clone();
    }

    /// <summary>
    /// Sets a typed value by converting it to JSON.
    /// </summary>
    public void Set<T>(string name, T value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
        Set(name, document.RootElement);
    }

    /// <summary>
    /// Gets a typed value.
    /// </summary>
    public T? Get<T>(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return values.TryGetValue(name, out var value) ? value.Deserialize<T>() : default;
    }

    /// <summary>
    /// Creates a persisted snapshot of this bag.
    /// </summary>
    public Dictionary<string, JsonElement> ToDictionary() => new(values, StringComparer.Ordinal);
}
