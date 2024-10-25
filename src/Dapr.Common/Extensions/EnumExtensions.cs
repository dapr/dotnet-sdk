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
            return value.ToString();

        var attributes = memberInfo[0].GetCustomAttributes(typeof(EnumMemberAttribute), false);
        return (attributes.Length > 0 ? ((EnumMemberAttribute)attributes[0]).Value : value.ToString()) ?? value.ToString();
    }
}
