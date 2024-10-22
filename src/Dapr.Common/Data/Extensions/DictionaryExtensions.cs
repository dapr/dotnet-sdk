namespace Dapr.Common.Data.Extensions;

/// <summary>
/// Provides extension methods for use with a <see cref="Dictionary{TKey,TValue}"/>.
/// </summary>
internal static class DictionaryExtensions
{
    /// <summary>
    /// Merges the keys and values of the provided dictionary in mergeFrom with the
    /// dictionary provided in mergeTo.
    /// </summary>
    /// <param name="mergeTo">The dictionary the values are being merged into.</param>
    /// <param name="mergeFrom">The dictionary the values are being merged from.</param>
    /// <typeparam name="TKey">The type of the key for either dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the value for either dictionary.</typeparam>
    internal static void MergeFrom<TKey, TValue>(this Dictionary<TKey, TValue> mergeTo,
        Dictionary<TKey, TValue> mergeFrom) where TKey : notnull
    {
        foreach (var kvp in mergeFrom)
        {
            mergeTo[kvp.Key] = kvp.Value;
        }
    }
}
