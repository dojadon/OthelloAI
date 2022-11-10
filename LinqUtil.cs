using System;
using System.Collections.Generic;
using System.Linq;

namespace OthelloAI
{
    public static class ArrayUtil
    {
        public static float[][] AverageAxis3(float[][][] a)
        {
            int l1 = a[0].Length;
            int l2 = a[0][0].Length;

            float[][] result = new float[l1][];
            for(int i = 0; i < result.Length; i++)
            {
                result[i] = new float[l2];

                for (int j = 0; j < result[i].Length; j++)
                    result[i][j] = a.Select(aa => aa[i][j]).Average();
            }

            return result;
        }
    }

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

        public static IEnumerable<int> Ranking<T>(this IEnumerable<T> source)
        {
            return source.Select((obj, i) => (obj, i)).OrderBy(t => t.obj).Select((t, i) => (t, i)).OrderBy(t => t.t.i).Select(t => t.i);
        }

        public static double Variance(this IEnumerable<int> source)
        {
            double a = source.Average();
            return source.Select(i => (i - a) * (i - a)).Average();
        }

        public static float Variance(this IEnumerable<float> source)
        {
            float a = source.Average();
            return source.Select(i => (i - a) * (i - a)).Average();
        }

        public static (double avg, double var) AverageAndVariance(this IEnumerable<int> source)
        {
            double a = source.Average();
            double v = source.Select(i => (i - a) * (i - a)).Average();
            return (a, v);
        }

        public static (float avg, float var) AverageAndVariance(this IEnumerable<float> source)
        {
            float a = source.Average();
            float v = source.Select(i => (i - a) * (i - a)).Average();
            return (a, v);
        }

        public static IEnumerable<TResult> ZipThree<T1, T2, T3, TResult>(
        this IEnumerable<T1> source,
        IEnumerable<T2> second,
        IEnumerable<T3> third,
        Func<T1, T2, T3, TResult> func)
        {
            using var e1 = source.GetEnumerator();
            using var e2 = second.GetEnumerator();
            using var e3 = third.GetEnumerator();

            while (e1.MoveNext() && e2.MoveNext() && e3.MoveNext())
                yield return func(e1.Current, e2.Current, e3.Current);
        }
    }

    public static class RandomUtil
    {
        public static ulong NextUInt64(this Random rand)
        {
            byte[] buf = new byte[8];
            rand.NextBytes(buf);
            return BitConverter.ToUInt64(buf, 0);
        }

        public static Board NextBoard(this Random rand)
        {
            ulong b = rand.NextUInt64();
            ulong w = rand.NextUInt64();

            ulong empty = b & w;
            b = b & ~empty;
            w = w & ~empty;

            return new Board(b, w);
        }

        public static T Choice<T>(this Random rand, List<T> list)
        {
            return list[rand.Next(list.Count)];
        }

        public static T Choice<T>(this Random rand, T[] array)
        {
            return array[rand.Next(array.Length)];
        }

        public static IEnumerable<T> Sample<T>(this Random rand, List<T> list, int n)
        {
            for (int i = 0; i < n; i++)
            {
                list[i] = list[i + rand.Next(list.Count - i)];
            }

            return list.Take(n);
        }

        public static (int, int) SamplePair(this Random rand, int size)
        {
            int i1 = rand.Next(size);
            int i2 = rand.Next(size - 1);

            if (i1 == i2)
                i2 = size - 1;

            return (i1, i2);
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
