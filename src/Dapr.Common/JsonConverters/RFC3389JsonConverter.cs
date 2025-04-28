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

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dapr.Common.JsonConverters;

/// <summary>
/// Provides serialization and deserialization between a <see cref="DateTimeOffset"/> and its RFC3389 format.
/// </summary>
internal sealed class Rfc3389JsonConverter : JsonConverter<DateTimeOffset?>
{
    private const string Rfc3389Format = "yyyy-MM-dd'T'HH:mm:ss.fffK";
    
    /// <summary>Reads and converts the JSON to type.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The converted value.</returns>
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var stringValue = reader.GetString();
        if (stringValue is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(stringValue))
        {
            throw new JsonException("The data string is empty or whitespace and cannot be converted to a DateTimeOffset.");
        }

        try
        {
            return DateTimeOffset.ParseExact(stringValue, Rfc3389Format, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal);
        }
        catch (FormatException ex)
        {
            throw new JsonException($"The date string '{stringValue}' is not in the expected RFC3389 format.", ex);
        }
    }

    /// <summary>Writes a specified value as JSON.</summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to convert to JSON.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        try
        {
            if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            { 
                var dateString = ((DateTimeOffset)value).ToString(Rfc3389Format, CultureInfo.InvariantCulture);
                var targetValue = dateString.Replace("+00:00", "Z").Trim('"');
                writer.WriteStringValue(targetValue);
            }
        }
        catch (Exception ex)
        {
            throw new JsonException("An error occurred while writing the DateTimeOffset value.", ex);
        }
    }
}
