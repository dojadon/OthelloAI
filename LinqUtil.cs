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

        public static IEnumerable<T> TakeLast<T>(this IEnumerable<T> source, int N)
        {
            return source.Skip(Math.Max(0, source.Count() - N));
        }
    }

    public static class RandomUtil
    {
        public static T Choice<T>(this Random rand, List<T> list)
        {
            return list[rand.Next(list.Count)];
        }

        public static IEnumerable<T> Sample<T>(this Random rand, List<T> list, int n)
        {
            for (int i = 0; i < n; i++)
            {
                list[i] = list[i + rand.Next(list.Count - i)];
            }

            return list.Take(n);
        }

        public static ulong GenerateRegion(this Random rand, int size, int n_1)
        {
            int n = 0;
            ulong b = 0;
            while (n < n_1)
            {
                ulong b_ = 1UL << rand.Next(size);
                if ((b & b_) != 0)
                    continue;

                n++;
                b |= b_;
            }
            return b;
        }

    }
}
