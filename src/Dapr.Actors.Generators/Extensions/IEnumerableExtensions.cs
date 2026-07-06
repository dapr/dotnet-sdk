namespace Dapr.Actors.Generators.Extensions;

internal static class IEnumerableExtensions
{
    /// <summary>
    /// Returns the index of the first item in the sequence that satisfies the predicate. If no item satisfies the predicate, -1 is returned.
    /// </summary>
    /// <typeparam name="T">The type of objects in the <see cref="IEnumerable{T}"/>.</typeparam>
    /// <param name="source"><see cref="IEnumerable{T}"/> in which to search.</param>
    /// <param name="predicate">Function performed to check whether an item satisfies the condition.</param>
    /// <returns>Return the zero-based index of the first occurrence of an element that satisfies the condition, if found; otherwise, -1.</returns>
    internal static int IndexOf<T>(this IEnumerable<T> source, Func<T, bool> predicate)
    {
        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        int index = 0;

        foreach (var item in source)
        {
            if (predicate(item))
            {
                return index;
            }

            index++;
        }

        return -1;
    }
}