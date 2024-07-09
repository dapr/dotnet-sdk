namespace Dapr.Actors.Generators.Extensions
{
    internal static class IEnumerableExtensions
    {
        /// <summary>
        /// Returns the index of the first item in the sequence that satisfies the predicate. If no item satisfies the predicate, -1 is returned.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="predicate"></param>
        /// <returns>Return the zero-based index of the first occurrence of an element that satisfies the condition, if found; otherwise, -1.</returns>
        public static int IndexOf<T>(this IEnumerable<T> source, Func<T, bool> predicate)
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

        /// <summary>
        /// Concatenates the specified item to the end of the sequence.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="item"></param>
        /// <returns></returns>
        public static IEnumerable<T> Concat<T>(this IEnumerable<T> source, T item)
        {
            return source.Concat(new[] { item });
        }
    }
}
