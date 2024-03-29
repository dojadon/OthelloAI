﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace OthelloAI
{
    public static class ArrayUtil
    {
        public static T[][] Divide<T>(T[] a, int n)
        {
            int len = a.Length / n;
            return Enumerable.Range(0, n).Select(i => a.Skip(i * len).Take(len).ToArray()).ToArray();
        }

        public static T[] PadLeft<T>(this T[] a, int length, T padding)
        {
            if(a.Length < length)
            {
                return a.Concat((length - a.Length).Loop(_ => padding)).ToArray();
            }
            else
            {
                return a.ToArray();
            }
        }
    }

    public static class LinqUtil
    {
        public static IEnumerable<int> Loop(this int i)
        {
            return Enumerable.Range(0, i);
        }

        public static IEnumerable<int> Loop(this int i, int start)
        {
            return Enumerable.Range(start, i);
        }

        public static IEnumerable<T> Loop<T>(this int i, Func<int, T> selector)
        {
            return i.Loop().Select(selector);
        }

        public static IEnumerable<T> Loop<T>(this int i, int start, Func<int, T> selector)
        {
            return i.Loop(start).Select(selector);
        }

        public static IEnumerable<T> ConcatOne<T>(this IEnumerable<T> first, T item)
        {
            return first.Concat(new[] { item });
        }

        public static IEnumerable<T> AsParallel<T>(this IEnumerable<T> first, bool is_parallel, int num_threads = -1)
        {
            if (is_parallel)
                if (num_threads > 0)
                    return first.AsParallel().WithDegreeOfParallelism(num_threads);
                else
                    return first.AsParallel();
            else
                return first;
        }

        public static IEnumerable<T> MultiConcat<T>(this IEnumerable<T> first, params IEnumerable<T>[] secondAndAfter)
        {
            return first.Concat(secondAndAfter.SelectMany(_ => _));
        }

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

    public class FixedQueue<T> : IEnumerable<T>
    {
        private Queue<T> _queue;

        public int Count => _queue.Count;

        public int Capacity { get; private set; }

        public FixedQueue(int capacity)
        {
            Capacity = capacity;
            _queue = new Queue<T>(capacity);
        }

        public void Enqueue(T item)
        {
            _queue.Enqueue(item);

            if (Count > Capacity) Dequeue();
        }

        public T Dequeue() => _queue.Dequeue();

        public T Peek() => _queue.Peek();

        public IEnumerator<T> GetEnumerator() => _queue.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _queue.GetEnumerator();
    }

    public static class RandomUtil
    {
        public static IEnumerable<T> Sample<T>(this Random random, IEnumerable<T> collection, int take)
        {
            var available = collection.Count();
            var needed = take;
            foreach (var item in collection)
            {
                if (random.Next(available) < needed)
                {
                    needed--;
                    yield return item;
                    if (needed == 0)
                    {
                        break;
                    }
                }
                available--;
            }
        }

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
