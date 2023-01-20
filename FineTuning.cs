using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OthelloAI
{
    public class FineTuner
    {
        public WeightArrayPextHashingTer[][] Source { get; }

        public FineTuner(WeightArrayPextHashingTer[][] source)
        {
            Source = source;
        }

        public void Apply(WeightArrayPextHashingTer[] dst, int n_ply)
        {
            FineTuning(Source[n_ply], dst);
        }

        public void Apply(WeightArrayPextHashingTer[] dst, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                FineTuning(Source[i], dst);
            }

            foreach (var w in dst)
                for (int i = 0; i < w.weights.Length; i++)
                    w.weights[i] /= (end - start);
        }

        public static void FineTuning(WeightArrayPextHashingTer[] src, WeightArrayPextHashingTer[] dst)
        {
            foreach (var w2 in src)
            {
                var w1 = dst.OrderByDescending(w1 => Board.BitCount(w1.mask & w2.mask)).First();
                WeightUtil.Test(w2.weights, w1.weights, w2.mask, w1.mask);
            }
        }
    }

    public static class WeightUtil
    {
        public static T[] ConcatWithMask<T>(T[] a1, T[] a2, bool[] mask)
        {
            T[] dst = new T[a1.Length + a2.Length];

            int i1 = 0;
            int i2 = 0;

            for (int i = 0; i < dst.Length; i++)
            {
                if (mask[i])
                    dst[i] = a1[i1++];
                else
                    dst[i] = a2[i2++];
            }

            return dst;
        }

        public static int Assemble(int[] discs)
        {
            int result = 0;
            for (int i = 0; i < discs.Length; i++)
            {
                result = result * 3 + discs[discs.Length - 1 - i];
            }
            return result;
        }

        public static int[] Disassemble(int hash, int length)
        {
            int[] result = new int[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = hash % 3;
                hash /= 3;
            }
            return result;
        }

        public static void Test(float[] w1, float[] w2, ulong m1, ulong m2)
        {
            int len_union = Board.BitCount(m1 & m2);

            if (len_union == 0)
            {
                float avg = w1.Average();
                for (int i = 0; i < w2.Length; i++)
                    w2[i] += avg;

                return;
            }

            int len_sub1 = Board.BitCount(m1 & ~m2);
            int len_sub2 = Board.BitCount(~m1 & m2);

            int n_union = (int)Math.Pow(3, len_union);
            int n_sub1 = (int)Math.Pow(3, len_sub1);
            int n_sub2 = (int)Math.Pow(3, len_sub2);

            static IEnumerable<ulong> DisassembleBits(ulong m)
            {
                ulong b;
                while ((b = Board.NextMove(m)) != 0)
                {
                    m = Board.RemoveMove(m, b);
                    yield return b;
                }
            }

            bool[] mask1 = DisassembleBits(m1).Select(b => (b & m2) != 0).ToArray();
            bool[] mask2 = DisassembleBits(m2).Select(b => (b & m1) != 0).ToArray();

            for (int i = 0; i < n_union; i++)
            {
                int[] a1 = Disassemble(i, len_union);

                float tmp = 0;

                for (int j = 0; j < n_sub1; j++)
                {
                    int[] a2 = Disassemble(j, len_sub1);
                    int[] a = ConcatWithMask(a1, a2, mask1);
                    tmp += w1[Assemble(a)];
                    // Console.WriteLine("src : " + string.Join(", ", a) + " : " + w1[Assemble(a)]);
                }
                tmp /= n_sub1;

                for (int j = 0; j < n_sub2; j++)
                {
                    int[] a2 = Disassemble(j, len_sub2);
                    int[] a = ConcatWithMask(a1, a2, mask2);
                    w2[Assemble(a)] += tmp;
                    // Console.WriteLine("dst : " + string.Join(", ", a) + " : " + tmp);
                }
            }
        }
    }
}
