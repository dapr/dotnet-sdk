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

using System.Reflection;
using System.Runtime.Serialization;

namespace Dapr.Common.Extensions;

internal static class EnumExtensions
{
    /// <summary>
    /// Reads the value of an enum out of the attached <see cref="EnumMemberAttribute"/> attribute.
    /// </summary>
    /// <typeparam name="T">The enum.</typeparam>
    /// <param name="value">The value of the enum to pull the value for.</param>
    /// <returns></returns>
    public static string GetValueFromEnumMember<T>(this T value) where T : Enum
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        var memberInfo = typeof(T).GetMember(value.ToString(), BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);
        if (memberInfo.Length <= 0)
        {
            return value.ToString();
        }

        var attributes = memberInfo[0].GetCustomAttributes(typeof(EnumMemberAttribute), false);
        return (attributes.Length > 0 ? ((EnumMemberAttribute)attributes[0]).Value : value.ToString()) ?? value.ToString();
    }

    /// <summary>
    /// Attempts to parse a string value into its Enum member equivalent.
    /// </summary>
    /// <param name="value">The value to parse.</param>
    /// <param name="result">If this method returns true, this contains the value of the matching enum member.</param>
    /// <typeparam name="TEnum">The type of enum to source the values from.</typeparam>
    /// <returns>True fi the value was successfully parsed; otherwise false.</returns>
    public static bool TryParseEnumMember<TEnum>(this string? value, out TEnum result)
    where TEnum: struct, Enum
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        
        // Try EnumMember(Value=...) first
        foreach (var name in Enum.GetNames(typeof(TEnum)))
        {
            var field = typeof(TEnum).GetField(name, BindingFlags.Public | BindingFlags.Static);
            var em = field?.GetCustomAttribute<EnumMemberAttribute>();
            if (em?.Value != null && string.Equals(em.Value, value, StringComparison.OrdinalIgnoreCase))
            {
                result = (TEnum)Enum.Parse(typeof(TEnum), name);
                return true;
            }
        }
        
        // Fallback: match to the enum identifier itself
        return Enum.TryParse(value, ignoreCase: true, out result);
    }

    /// <summary>
    /// Attempts to parse a string value into its Enum member equivalent.
    /// </summary>
    /// <param name="value">The value to parse.</param>
    /// <typeparam name="TEnum">The type of enum to source the values from.</typeparam>
    /// <returns>The value of the matching enum member.</returns>
    public static TEnum ParseEnumMember<TEnum>(string value)
        where TEnum : struct, Enum => TryParseEnumMember<TEnum>(value, out var r)
        ? r
        : throw new ArgumentException($"Cannot map '{value}' to {typeof(TEnum).Name} via EnumMember or name.",
            nameof(value));
}
