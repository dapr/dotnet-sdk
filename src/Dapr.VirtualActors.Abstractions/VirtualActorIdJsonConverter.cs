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
// ------------------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dapr.VirtualActors;

/// <summary>
/// JSON converter for <see cref="VirtualActorId"/> that serializes and deserializes actor IDs as strings.
/// </summary>
public sealed class VirtualActorIdJsonConverter : JsonConverter<VirtualActorId>
{
    /// <inheritdoc />
    public override VirtualActorId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var id = reader.GetString();
        return id is not null ? new VirtualActorId(id) : default;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, VirtualActorId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.GetId());
    }
}
