// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using Dapr.Common.Extensions;

namespace Dapr.Common.JsonConverters;

/// <summary>
/// A JsonConverter used to convert from an enum to a string and vice versa, but using the Enum extension written to pull
/// the value from the [EnumMember] attribute, if present.
/// </summary>
/// <typeparam name="T">The enum type to convert.</typeparam>
internal sealed class GenericEnumJsonConverter<T> : JsonConverter<T> where T : struct, Enum
{
    /// <summary>Reads and converts the JSON to type <typeparamref name="T" />.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The converted value.</returns>
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        //Get the string value from the JSON reader
        var value = reader.GetString();

        //Loop through all the enum values
        foreach (var enumValue in Enum.GetValues<T>())
        {
            //Get the value from the EnumMember attribute, if any
            var enumMemberValue = enumValue.GetValueFromEnumMember();

            //If the values match, return the enum value
            if (value == enumMemberValue)
            {
                return enumValue;
            }
        }

        //If no match found, throw an exception
        throw new JsonException($"Invalid valid for {typeToConvert.Name}: {value}");
    }

    /// <summary>Writes a specified value as JSON.</summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to convert to JSON.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        //Get the value from the EnumMember attribute, if any
        var enumMemberValue = value.GetValueFromEnumMember();

        //Write the value to the JSON writer
        writer.WriteStringValue(enumMemberValue);
    }
}
