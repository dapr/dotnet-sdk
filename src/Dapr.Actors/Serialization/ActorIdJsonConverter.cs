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

namespace Dapr.Actors.Seralization;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

// Converter for ActorId - will be serialized as a JSON string
internal class ActorIdJsonConverter : JsonConverter<ActorId>
{
    public override ActorId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert != typeof(ActorId))
        {
            throw new ArgumentException( $"Conversion to the type '{typeToConvert}' is not supported.", nameof(typeToConvert));
        }

        // Note - we generate random Guids for Actor Ids when we're generating them randomly
        // but we don't actually enforce a format. Ids could be a number, or a date, or whatever,
        // we don't really care. However we always **represent** Ids in JSON as strings.
        if (reader.TokenType == JsonTokenType.String && 
            reader.GetString() is string text && 
            !string.IsNullOrWhiteSpace(text))
        {
            return new ActorId(text);
        }
        else if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        throw new JsonException(); // The serializer will provide a default error message.
    }

    public override void Write(Utf8JsonWriter writer, ActorId value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value.GetId());
        }
    }
}