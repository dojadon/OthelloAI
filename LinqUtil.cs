using System;
using System.Collections.Generic;
using System.Linq;

namespace OthelloAI
{
    public static class LinqUtil
    {
        public static IEnumerable<T> MinBy<T, U>(this IEnumerable<T> source, Func<T, U> selector)
        {
            var lookup = source.ToLookup(selector);
            return lookup[lookup.Min(a => a.Key)];
        }

        public static IEnumerable<T> MaxBy<T, U>(this IEnumerable<T> source, Func<T, U> selector)
        {
            var lookup = source.ToLookup(selector);
            return lookup[lookup.Max(a => a.Key)];
        }
    }
}
