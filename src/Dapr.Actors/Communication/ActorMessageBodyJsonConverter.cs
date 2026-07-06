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

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dapr.Actors.Communication;

internal class ActorMessageBodyJsonConverter<T> : JsonConverter<T>
{
    private readonly List<Type> methodRequestParameterTypes;
    private readonly List<Type> wrappedRequestMessageTypes;
    private readonly Type wrapperMessageType;

    public ActorMessageBodyJsonConverter(
        List<Type> methodRequestParameterTypes,
        List<Type> wrappedRequestMessageTypes = null
    )
    {
        this.methodRequestParameterTypes = methodRequestParameterTypes;
        this.wrappedRequestMessageTypes = wrappedRequestMessageTypes;

        if (this.wrappedRequestMessageTypes != null && this.wrappedRequestMessageTypes.Count == 1)
        {
            this.wrapperMessageType = this.wrappedRequestMessageTypes[0];
        }
    }

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Ensure start-of-object, then advance
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();
        reader.Read();

        // Ensure property name, then advance
        if (reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "value") throw new JsonException();
        reader.Read();

        // If the value is null, return null.
        if (reader.TokenType == JsonTokenType.Null)
        {
            // Read the end object token.
            reader.Read();
            return default;
        }

        // If the value is an object, deserialize it to wrapper message type
        if (this.wrapperMessageType != null)
        {
            var value = JsonSerializer.Deserialize(ref reader, this.wrapperMessageType, options);

            // Construct a new WrappedMessageBody with the deserialized value.
            var wrapper = new WrappedMessageBody()
            {
                Value = value,
            };

            // Read the end object token.
            reader.Read();

            // Coerce the type to T; required because WrappedMessageBody inherits from two separate interfaces, which
            // cannot both be used as generic constraints
            return (T)(object)wrapper;
        }

        return JsonSerializer.Deserialize<T>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("value");

        if (value is WrappedMessageBody body)
        {
            JsonSerializer.Serialize(writer, body.Value, body.Value.GetType(), options);
        }
        else
            writer.WriteNullValue();
        writer.WriteEndObject();
    }
}