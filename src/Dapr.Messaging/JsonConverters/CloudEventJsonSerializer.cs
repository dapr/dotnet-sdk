// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using Dapr.Messaging.PublishSubscribe;

namespace Dapr.Messaging.JsonConverters;

internal sealed class CloudEventDataConverterFactory(string dataContentType) : JsonConverterFactory
{
    /// <summary>When overridden in a derived class, determines whether the converter instance can convert the specified object type.</summary>
    /// <param name="typeToConvert">The type of the object to check whether it can be converted by this converter instance.</param>
    /// <returns>
    /// <see langword="true" /> if the instance can convert the specified object type; otherwise, <see langword="false" />.</returns>
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(object);

    /// <summary>Creates a converter for a specified type.</summary>
    /// <param name="typeToConvert">The type handled by the converter.</param>
    /// <param name="options">The serialization options to use.</param>
    /// <returns>A converter for which <typeparamref name="T" /> is compatible with <paramref name="typeToConvert" />.</returns>
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        new CloudEventDataConverter(dataContentType);
}

internal sealed class CloudEventJsonSerializer<T> : JsonConverter<CloudEvent<T>>
{
    /// <summary>Reads and converts the JSON to type <typeparamref name="T" />.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The converted value.</returns>
    public override CloudEvent<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    /// <summary>Writes a specified value as JSON.</summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to convert to JSON.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void Write(Utf8JsonWriter writer, CloudEvent<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        if (value.DataContentType == "application/json")
        {
            writer.WritePropertyName("Data");
            JsonSerializer.Serialize(writer, value.Data, options);
        }
        else
        {
            writer.WriteString("Data", value.Data?.ToString());
        }
        
        writer.WriteEndObject();
    }
}
