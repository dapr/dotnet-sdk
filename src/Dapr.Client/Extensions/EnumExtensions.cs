// ------------------------------------------------------------------------
// Copyright 2023 The Dapr Authors
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

#nullable enable
using System;
using System.Reflection;
using System.Runtime.Serialization;

namespace Dapr.Client;

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
            return value.ToString();

        var attributes = memberInfo[0].GetCustomAttributes(typeof(EnumMemberAttribute), false);
        return (attributes.Length > 0 ? ((EnumMemberAttribute)attributes[0]).Value : value.ToString()) ?? value.ToString();
    }
}