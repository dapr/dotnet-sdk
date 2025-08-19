using System.Collections;
using System.Text.Json;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;

namespace Dapr.AI.Conversation.Extensions;

/// <summary>
/// Provides static helper extension methods for Protobuf types.
/// </summary>
internal static class ProtobufHelpers
{
    /// <summary>
    /// Creates an <see cref="RepeatedField{T}"/> from an <see cref="IEnumerable{T}"/>.
    /// </summary>
    /// <param name="items"></param>
    /// <returns></returns>
    public static RepeatedField<T> ToRepeatedField<T>(this IEnumerable<T> items)
    {
        var field = new RepeatedField<T>();
        field.AddRange(items);
        return field;
    }
    
    /// <summary>
    /// Creates a <see cref="Value"/> from an object.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns></returns>
    public static Value ToValue(object? value) =>
        value switch
        {
            null => new Value { NullValue = NullValue.NullValue },

            // Scalars
            string s => new Value { StringValue = s },
            bool b => new Value { BoolValue = b },
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal
                => new Value { NumberValue = Convert.ToDouble(value) },

            // JSON-native types
            JsonElement je => FromJsonElement(je),

            // Dictionaries map to Struct
            IDictionary<string, object?> dict => new Value { StructValue = ToStruct(dict) },
            IEnumerable<KeyValuePair<string, object?>> kvps => new Value { StructValue = ToStruct(kvps) },

            // Enumerables map to ListValue
            IEnumerable enumerable => new Value { ListValue = ToListValue(enumerable) },

            // Fallback: serialize arbitrary POCO to JSON, then convert
            _ => FromJsonElement(JsonSerializer.SerializeToElement(value))
        };
    
    /// <summary>
    /// Maps an <see cref="IEnumerable{T}"/> into a <see cref="Struct"/>.
    /// </summary>
    /// <param name="kvps">The key/value pairs to map.</param>
    /// <returns></returns>
    private static Struct ToStruct(IEnumerable<KeyValuePair<string, object?>> kvps)
    {
        var s = new Struct();
        foreach (var (k, v) in kvps)
        {
            s.Fields[k] = ToValue(v);
        }

        return s;
    }

    /// <summary>
    /// Maps an <see cref="IDictionary{K, V}"/> into a <see cref="Struct"/>.
    /// </summary>
    /// <param name="dict">The dictionary to map.</param>
    /// <returns></returns>
    private static Struct ToStruct(IDictionary<string, object?> dict)
    {
        var s = new Struct();
        foreach (var kvp in dict)
        {
            s.Fields[kvp.Key] = ToValue(kvp.Value);
        }

        return s;
    }

    /// <summary>
    /// Maps an <see cref="IEnumerable{T}"/> into a <see cref="ListValue"/>.
    /// </summary>
    /// <param name="items">The items to map.</param>
    /// <returns></returns>
    private static ListValue ToListValue(IEnumerable items)
    {
        var lv = new ListValue();
        foreach (var item in items)
        {
            lv.Values.Add(ToValue(item));
        }

        return lv;
    }

    /// <summary>
    /// Maps a <see cref="JsonElement"/> into a <see cref="Value"/>.
    /// </summary>
    /// <param name="je">The JSON element to map.</param>
    /// <returns></returns>
    private static Value FromJsonElement(JsonElement je) =>
        je.ValueKind switch
        {
            JsonValueKind.Null => new Value { NullValue = NullValue.NullValue },
            JsonValueKind.True => new Value { BoolValue = true },
            JsonValueKind.False => new Value { BoolValue = false },
            JsonValueKind.Number => new Value
            {
                NumberValue = je.TryGetDouble(out var d) ? d : Convert.ToDouble(je.GetDecimal())
            },
            JsonValueKind.String => new Value { StringValue = je.GetString()! },
            JsonValueKind.Object => new Value { StructValue = FromJsonObject(je) },
            JsonValueKind.Array => new Value { ListValue = FromJsonArray(je) },
            _ => new Value { NullValue = NullValue.NullValue }
        };

    /// <summary>
    /// Maps a <see cref="JsonElement"/> object into a <see cref="Struct"/>./>
    /// </summary>
    /// <param name="obj">The object to map.</param>
    /// <returns></returns>
    private static Struct FromJsonObject(JsonElement obj)
    {
        var s = new Struct();
        foreach (var prop in obj.EnumerateObject())
        {
            s.Fields[prop.Name] = FromJsonElement(prop.Value);
        }

        return s;
    }

    /// <summary>
    /// Maps a <see cref="JsonElement"/> array into a <see cref="ListValue"/>.
    /// </summary>
    /// <param name="arr">The array of JSON elements to map.</param>
    /// <returns></returns>
    private static ListValue FromJsonArray(JsonElement arr)
    {
        var lv = new ListValue();
        foreach (var item in arr.EnumerateArray())
        {
            lv.Values.Add(FromJsonElement(item));
        }

        return lv;
    }
}
