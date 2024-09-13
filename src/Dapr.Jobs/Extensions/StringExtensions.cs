namespace Dapr.Jobs.Extensions;

internal static class StringExtensions
{
    /// <summary>
    /// Extension method that validates a string against a list of possible matches.
    /// </summary>
    /// <param name="value">The string value to evaluate.</param>
    /// <param name="possibleValues">The possible values to look for a match within.</param>
    /// <returns></returns>
    public static bool EndsWithAny(this string value, IReadOnlyList<string> possibleValues)
    {
        return possibleValues.Select(value.EndsWith).FirstOrDefault();
    }
}
