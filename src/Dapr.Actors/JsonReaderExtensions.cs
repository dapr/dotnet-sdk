// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Xml;
    using Newtonsoft.Json;

    /// <summary>
    /// Defines extension methods for JsonReader.
    /// </summary>
    internal static class JsonReaderExtensions
    {
        /// <summary>
        /// Moves the JSON reader token to next content.
        /// </summary>
        /// <param name="reader">The JsonReader.</param>
        public static void MoveToContent(this JsonReader reader)
        {
            while ((reader.TokenType == JsonToken.Comment || reader.TokenType == JsonToken.None) && reader.Read())
            {
            }
        }

        /// <summary>
        /// Reads StartObject token, throws if its not.
        /// </summary>
        /// <param name="reader">The JsonReader.</param>
        public static void ReadStartObject(this JsonReader reader)
        {
            reader.MoveToContent();
            if (reader.TokenType != JsonToken.StartObject)
            {
                throw new JsonReaderException($"Unexpected JsonToken {reader.TokenType}.");
            }

            reader.Read();
        }

        /// <summary>
        /// Reads EndObject token, throws if its not.
        /// </summary>
        /// <param name="reader">The JsonReader.</param>
        public static void ReadEndObject(this JsonReader reader)
        {
            reader.MoveToContent();
            if (reader.TokenType != JsonToken.EndObject)
            {
                throw new JsonReaderException($"Unexpected JsonToken {reader.TokenType}.");
            }

            reader.Read();
        }

        /// <summary>
        /// Reads StartArray token, throws if its not.
        /// </summary>
        /// <param name="reader">The JsonReader.</param>
        public static void ReadStartArray(this JsonReader reader)
        {
            reader.MoveToContent();
            if (reader.TokenType != JsonToken.StartArray)
            {
                throw new JsonReaderException($"Unexpected JsonToken {reader.TokenType}.");
            }

            reader.Read();
        }

        /// <summary>
        /// Reads EndArray token, throws if its not.
        /// </summary>
        /// <param name="reader">The JsonReader.</param>
        public static void ReadEndArray(this JsonReader reader)
        {
            reader.MoveToContent();
            if (reader.TokenType != JsonToken.EndArray)
            {
                throw new JsonReaderException($"Unexpected JsonToken {reader.TokenType}.");
            }

            reader.Read();
        }

        /// <summary>
        /// Reads PropertyName token, throws if its not.
        /// </summary>
        /// <param name="reader">The JsonReader.</param>
        /// <returns>NameDescription of property.</returns>
        public static string ReadPropertyName(this JsonReader reader)
        {
            if (reader.TokenType != JsonToken.PropertyName)
            {
                throw new JsonReaderException($"Error reading Property NameDescription from Json, unexpected JsonToken {reader.TokenType}.");
            }

            var propName = reader.Value?.ToString();
            reader.Read();
            return propName;
        }

        /// <summary>
        /// Gets the value of current JSON token as <see cref="string"/> and moves to next token".
        /// </summary>
        /// <param name="reader">Json reader.</param>
        /// <returns>A <see cref="string" />.</returns>
        public static string ReadValueAsString(this JsonReader reader)
        {
            string value = null;
            switch (reader.TokenType)
            {
                case JsonToken.String:
                    value = (string)reader.Value;
                    break;
                case JsonToken.Integer:
                case JsonToken.Float:
                case JsonToken.Boolean:
                case JsonToken.Date:
                    if (reader.Value is IFormattable formattable)
                    {
                        value = formattable.ToString();
                    }

                    break;
                case JsonToken.Null:
                    // value is initialized to null
                    break;
                default:
                    throw new JsonReaderException($"Error reading string. Unexpected token: {reader.TokenType}.");
            }

            reader.Read();
            return value;
        }

        /// <summary>
        /// Gets the value of current JSON token as <see cref="bool"/> and moves to next token".
        /// </summary>
        /// <param name="reader">Json reader.</param>
        /// <returns>A <see cref="bool" />.</returns>
        public static bool? ReadValueAsBool(this JsonReader reader)
        {
            bool? value = null;

            switch (reader.TokenType)
            {
                case JsonToken.Boolean:
                    value = Convert.ToBoolean(reader.Value);
                    break;
                case JsonToken.String:
                    value = ParseAsType((string)reader.Value, bool.Parse);
                    break;
                case JsonToken.Null:
                    // value is initialized to null
                    break;
                default:
                    throw new JsonReaderException($"Error reading boolean. Unexpected token: {reader.TokenType}.");
            }

            reader.Read();
            return value;
        }

        /// <summary>
        /// Gets the value of current JSON token as <see cref="int"/> and moves to next token".
        /// </summary>
        /// <param name="reader">Json reader.</param>
        /// <returns>A <see cref="int" />.</returns>
        public static int? ReadValueAsInt(this JsonReader reader)
        {
            int? value = null;

            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                    value = Convert.ToInt32(reader.Value);
                    break;

                case JsonToken.String:
                    value = ParseAsTypeWithInvariantCulture((string)reader.Value, int.Parse);
                    break;

                case JsonToken.Null:
                    // value is initialized to null
                    break;

                default:
                    throw new JsonReaderException($"Error reading integer. Unexpected token: {reader.TokenType}.");
            }

            reader.Read();
            return value;
        }

        /// <summary>
        /// Gets the value of current JSON token as <see cref="byte"/> and moves to next token".
        /// </summary>
        /// <param name="reader">Json reader.</param>
        /// <returns>A <see cref="int" />.</returns>
        public static byte ReadValueAsByte(this JsonReader reader)
        {
            // byte is int in JSON.
            int value;

            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                    value = Convert.ToInt32(reader.Value);
                    break;

                case JsonToken.String:
                    value = ParseAsTypeWithInvariantCulture((string)reader.Value, int.Parse);
                    break;

                default:
                    throw new JsonReaderException($"Error reading integer. Unexpected token: {reader.TokenType}.");
            }

            reader.Read();
            return (byte)value;
        }

        /// <summary>
        /// Gets the value of current JSON token as <see cref="long"/> and moves to next token".
        /// </summary>
        /// <param name="reader">Json reader.</param>
        /// <returns>A <see cref="long" />.</returns>
        public static long? ReadValueAsLong(this JsonReader reader)
        {
            long? value = null;

            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                    value = Convert.ToInt64(reader.Value);
                    break;
                case JsonToken.String:
                    value = ParseAsTypeWithInvariantCulture((string)reader.Value, long.Parse);
                    break;
                case JsonToken.Null:
                    // value is initialized to null
                    break;
                default:
                    throw new JsonReaderException($"Error reading integer. Unexpected token: {reader.TokenType}.");
            }

            reader.Read();
            return value;
        }

        /// <summary>
        /// Gets the value of current JSON token as <see cref="double"/> and moves to next token".
        /// </summary>
        /// <param name="reader">Json reader.</param>
        /// <returns>A <see cref="double" />.</returns>
        public static double? ReadValueAsDouble(this JsonReader reader)
        {
            double? value = null;

            switch (reader.TokenType)
            {
                case JsonToken.Float:
                    value = Convert.ToDouble(reader.Value);
                    break;
                case JsonToken.String:
                    value = ParseAsTypeWithInvariantCulture((string)reader.Value, double.Parse);
                    break;
                case JsonToken.Null:
                    // value is initialized to null
                    break;
                default:
                    throw new JsonReaderException($"Error reading integer. Unexpected token: {reader.TokenType}.");
            }

            reader.Read();
            return value;
        }

        /// <summary>
        /// Gets the value of current JSON token as <see cref="System.DateTime"/> and moves to next token".
        /// </summary>
        /// <param name="reader">Json reader.</param>
        /// <returns>A <see cref="System.DateTime" />.</returns>
        public static DateTime? ReadValueAsDateTimeISO8601Format(this JsonReader reader)
        {
            // DateTime is a string in ISO8601 format
            DateTime? value = null;

            switch (reader.TokenType)
            {
                case JsonToken.Date:
                    value = (DateTime)reader.Value;
                    break;
                case JsonToken.String:
                    var valueString = (string)reader.Value;
                    try
                    {
                        value = XmlConvert.ToDateTime(valueString, XmlDateTimeSerializationMode.Utc);
                    }
                    catch (Exception)
                    {
                        // TODO: try parsing with DateTime.Parse, Remove it once all apis return in ISO8601 format.
                        try
                        {
                            value = DateTime.Parse(valueString);
                        }
                        catch (Exception ex)
                        {
                            throw new JsonReaderException(
                                $"Error converting string to System.DateTime, string value to be converted is {valueString}.  DateTime values must be specified in string as per ISO8601",
                                ex);
                        }
                    }

                    break;

                case JsonToken.Null:
                    // value is initialized to null
                    break;

                default:
                    throw new JsonReaderException($"Error reading Date. Unexpected token: {reader.TokenType}.");
            }

            reader.Read();
            return value;
        }

        /// <summary>
        /// Gets the value of current JSON token as <see cref="System.TimeSpan"/> and moves to next token".
        /// </summary>
        /// <param name="reader">Json reader.</param>
        /// <returns>A <see cref="System.TimeSpan" />.</returns>
        public static TimeSpan? ReadValueAsTimeSpanISO8601Format(this JsonReader reader)
        {
            // TimeSpan is a string in ISO8601 format
            TimeSpan? value = null;

            switch (reader.TokenType)
            {
                case JsonToken.String:
                    var valueString = (string)reader.Value;
                    try
                    {
                        value = XmlConvert.ToTimeSpan(valueString);
                    }
                    catch (Exception)
                    {
                        // TODO: try parsing with DateTime.Parse, Remove it once all apis return in ISO8601 format.
                        try
                        {
                            value = TimeSpan.Parse(valueString);
                        }
                        catch (Exception ex)
                        {
                            throw new JsonReaderException(
                            $"Error converting string to System.TimeSpan, string value to be converted is {valueString}. Timespan values must be specified in string as per ISO8601.",
                            ex);
                        }
                    }

                    break;
                case JsonToken.Null:
                    // value is initialized to null
                    break;

                default:
                    {
                        throw new JsonReaderException($"Error reading TimeSpan. Unexpected token: {reader.TokenType}.");
                    }
            }

            reader.Read();
            return value;
        }

        /// <summary>
        /// Gets the value of current JSON token as <see cref="System.TimeSpan"/> and moves to next token".
        /// </summary>
        /// <param name="reader">Json reader.</param>
        /// <returns>A <see cref="System.TimeSpan" />.</returns>
        public static TimeSpan? ReadValueAsTimeSpanDaprFormat(this JsonReader reader)
        {
            // TimeSpan is a string. Format returned by Dapr is: 1h4m5s4ms4us4ns
            //  acceptable values are: m, s, ms, us(micro), ns
            TimeSpan? value = null;

            switch (reader.TokenType)
            {
                case JsonToken.String:
                    var valueString = (string)reader.Value;
                    var spanOfValue = valueString.AsSpan();
                    try
                    {
                        // Change the value returned by Dapr runtime, so that it can be parsed with TimeSpan.
                        // Format returned by Dapr runtime: 4h15m50s60ms. It doesnt have days.
                        // Dapr runtime should handle timespans in ISO 8601 format.
                        // Replace ms before m & s. Also append 0 days for parsing correctly with TimeSpan
                        int hIndex = spanOfValue.IndexOf('h');
                        int mIndex = spanOfValue.IndexOf('m');
                        int sIndex = spanOfValue.IndexOf('s');
                        int msIndex = spanOfValue.IndexOf("ms");

                        // handle days from hours.
                        var hoursSpan = spanOfValue.Slice(0, hIndex);
                        var hours = int.Parse(hoursSpan);
                        var days = hours / 24;
                        hours = hours % 24;

                        var minutesSpan = spanOfValue.Slice(hIndex + 1, mIndex - (hIndex + 1));
                        var minutes = int.Parse(minutesSpan);

                        var secondsSpan = spanOfValue.Slice(mIndex + 1, sIndex - (mIndex + 1));
                        var seconds = int.Parse(secondsSpan);

                        var millisecondsSpan = spanOfValue.Slice(sIndex + 1, msIndex - (sIndex + 1));
                        var milliseconds = int.Parse(millisecondsSpan);

                        value = new TimeSpan(days, hours, minutes, seconds, milliseconds);
                    }
                    catch (Exception ex)
                    {
                        throw new JsonReaderException(
                        $"Error converting string to System.TimeSpan, string value to be converted is {valueString}..", ex);
                    }

                    break;
                case JsonToken.Null:
                    // value is initialized to null
                    break;

                default:
                    {
                        throw new JsonReaderException($"Error reading TimeSpan. Unexpected token: {reader.TokenType}.");
                    }
            }

            reader.Read();
            return value;
        }

        /// <summary>
        /// Gets the value of current JSON token as <see cref="System.Guid"/> and moves to next token".
        /// </summary>
        /// <param name="reader">Json reader.</param>
        /// <returns>A <see cref="System.Guid" />.</returns>
        public static Guid? ReadValueAsGuid(this JsonReader reader)
        {
            Guid? value = null;

            switch (reader.TokenType)
            {
                case JsonToken.String:
                    value = ParseAsType((string)reader.Value, Guid.Parse);
                    break;
                case JsonToken.Null:
                    // value is initialized to null
                    break;
                default:
                    throw new JsonReaderException($"Error reading Date. Unexpected token: {reader.TokenType}.");
            }

            reader.Read();
            return value;
        }

        /// <summary>
        /// Skips a property value.
        /// </summary>
        /// <param name="reader">Json reader.</param>
        public static void SkipPropertyValue(this JsonReader reader)
        {
            if (reader.TokenType.Equals(JsonToken.StartObject) || reader.TokenType.Equals(JsonToken.StartArray))
            {
                reader.Skip();
            }

            reader.Read();
        }

        /// <summary>
        /// Reads a JSON array.
        /// </summary>
        /// <typeparam name="T">Type of list elements.</typeparam>
        /// <param name="reader">The JsonReader object.</param>
        /// <param name="deserializerFunc">Func to deserialize T.</param>
        /// <returns>Returns the List of T.</returns>
        public static List<T> ReadList<T>(this JsonReader reader, Func<JsonReader, T> deserializerFunc)
        {
            // handle null.
            if (reader.TokenType == JsonToken.Null)
            {
                reader.Read();
                return null;
            }

            var value = new List<T>();
            reader.ReadStartArray();

            do
            {
                // handle empty array.
                if (reader.TokenType == JsonToken.EndArray)
                {
                    break;
                }

                var item = deserializerFunc(reader);
                value.Add(item);
            }
            while (reader.TokenType != JsonToken.EndArray);

            reader.ReadEndArray();
            return value;
        }

        /// <summary>
        /// Reads a Dictionary from JSON.
        /// </summary>
        /// <typeparam name="T">Type of Dictionary values.</typeparam>
        /// <param name="reader">The JsonReader object.</param>
        /// <param name="deserializerFunc">Func to deserialize T.</param>
        /// <returns>Returns the dictionary.</returns>
        public static Dictionary<string, T> ReadDictionary<T>(this JsonReader reader, Func<JsonReader, T> deserializerFunc)
        {
            // handle null.
            if (reader.TokenType == JsonToken.Null)
            {
                reader.Read();
                return null;
            }

            var dict = new Dictionary<string, T>();
            reader.ReadStartObject();

            do
            {
                // handle empty dictionary.
                if (reader.TokenType == JsonToken.EndObject)
                {
                    break;
                }

                // key is propertyName, read property value and move to next token.
                var key = reader.ReadPropertyName();
                var value = deserializerFunc(reader);
                dict.Add(key, value);
            }
            while (reader.TokenType != JsonToken.EndObject);

            reader.ReadEndObject();
            return dict;
        }

        /// <summary>
        /// Deserializes Json representing of type T.
        /// </summary>
        /// <typeparam name="T">Type to deserialize into.</typeparam>
        /// <param name="reader">Json Reader.</param>
        /// <param name="getFromJsonPropertiesFunc">Delegate to parse JSON properties for type T.</param>
        /// <returns>Deserialized object of type T. returns default(T) if Json Token represented by reader is null
        /// OR its an empty Json.</returns>
        public static T Deserialize<T>(this JsonReader reader, Func<JsonReader, T> getFromJsonPropertiesFunc)
        {
            var obj = default(T);

            // handle null.
            if (reader.TokenType.Equals(JsonToken.Null))
            {
                reader.Read();
                return obj;
            }

            // Handle JsonReader created over stream of length 0.
            reader.MoveToContent();
            if (reader.TokenType.Equals(JsonToken.None))
            {
                return obj;
            }

            // handle Empty Json.
            reader.ReadStartObject();
            if (reader.TokenType.Equals(JsonToken.EndObject))
            {
                reader.ReadEndObject();
                return obj;
            }

            // not empty JSON, get value by reading properties.
            obj = getFromJsonPropertiesFunc.Invoke(reader);
            reader.ReadEndObject();
            return obj;
        }

        /// <summary>
        /// Parses type T from string value.
        /// </summary>
        /// <typeparam name="T">Type T to parse value as.</typeparam>
        /// <param name="value">value to parse.</param>
        /// <param name="parseFunc">Parse Function.</param>
        /// <returns>Parsed value.</returns>
        private static T ParseAsType<T>(string value, Func<string, T> parseFunc)
        {
            T result;

            try
            {
                result = parseFunc(value);
            }
            catch (Exception ex)
            {
                throw new JsonReaderException(
                    $"Error converting string to {typeof(T)}, string value to be converted is {value}", ex);
            }

            return result;
        }

        /// <summary>
        /// Parses type T from string value.
        /// </summary>
        /// <typeparam name="T">Type T to parse value as.</typeparam>
        /// <param name="value">value to parse.</param>
        /// <param name="parseFunc">Parse Function.</param>
        /// <returns>Parsed value.</returns>
        private static T ParseAsTypeWithInvariantCulture<T>(string value, Func<string, CultureInfo, T> parseFunc)
        {
            T result;

            try
            {
                result = parseFunc(value, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                throw new JsonReaderException(
                    $"Error converting string to {typeof(T)}, string value to be converted is {value}", ex);
            }

            return result;
        }
    }
}
