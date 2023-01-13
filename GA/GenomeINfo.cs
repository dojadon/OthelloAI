using MathNet.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OthelloAI.GA
{
    public interface IGenomeGenerator<T>
    {
        GenomeTuple<T>[][] Generate(GenomeInfo<T> info, Random rand);
    }



    public class GenomeInfo<T>
    {
        public Func<Random, T> GenomeGenerator { get; init; }
        public Func<T, int, ulong> Decoder { get; init; }

        public int NumStages { get; init; }
        public int NumTuples { get; init; }

        public float MinDepth { get; init; }

        public int[][] NetworkSizes { get; set; }

        public static int[][] CreateSimpleNetworkSizes(int size_min, int size_max, int max_n_weights)
        {
            bool CheckSize(IEnumerable<int> list)
            {
                return list.Sum(i => i == 0 ? 0 : Math.Pow(3, i)) <= max_n_weights;
            }

            IEnumerable<int[]> Func(int[] list)
            {
                for (int i = size_min; i <= size_max; i++)
                {
                    if (list.Length > 0 && i < list[^1])
                        continue;

                    var next = list.Concat(new[] { i }).ToArray();

                    if (CheckSize(next))
                    {
                        foreach (var l in Func(next))
                            yield return l;
                    }
                    else
                    {
                        yield return list;
                        yield break;
                    }
                }
            }

            var sizes = Func(Array.Empty<int>()).ToArray();

            bool IsDominated(int[] s1, int[] s2)
            {
                return s1.Length == s2.Length && s1.Zip(s2).All(t => t.First <= t.Second);
            }

            bool IsDominatedAny(int[] s)
            {
                return sizes.Count(s2 => IsDominated(s, s2)) > 1;
            }

            return sizes.Where(s => !IsDominatedAny(s)).ToArray();
        }

        public Individual<T> Generate(Random rand)
        {
            int[] sizes = rand.Choice(NetworkSizes);

            GenomeTuple<T> CreateGenome(int i)
            {
                int size = i < sizes.Length ? sizes[i] : -1;
                return new GenomeTuple<T>(GenomeGenerator(rand), size);
            }

            return new Individual<T>(Enumerable.Range(0, NumStages).Select(_ => Enumerable.Range(0, NumTuples).Select(i => CreateGenome(i)).ToArray()).ToArray(), this);
        }

        public float CalcExeCost(int n_tuples)
        {
            float t_factor = 20F;
            float cost_per_node = 480F;

            return cost_per_node + n_tuples * t_factor;
        }

        public const int MAX_NUM_TUPLES = 9;

        public float GetDepth(int n_tuples)
        {
            float max_t = CalcExeCost(MAX_NUM_TUPLES);
            float t = CalcExeCost(n_tuples);

            return (float)Math.Log(max_t / t) / 1.1F + MinDepth;
        }
    }
}
