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
        public int SizeMax { get; init; }
        public int SizeMin { get; init; }

        public int MaxNumWeights { get; init; }

        public float MinDepth { get; init; }

        public int[][] TupleSizeCombinations { get; set; }

        public void Init()
        {
            bool CheckSize(IEnumerable<int> list)
            {
                return list.Sum(i => i == 0 ? 0 : Math.Pow(3, i)) <= MaxNumWeights;
            }

            IEnumerable<int[]> Func(int[] list)
            {
                for (int i = SizeMin; i <= SizeMax; i++)
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

            bool IsDominated(int[] s1, int[] s2)
            {
                return s1.Length == s2.Length && s1.Zip(s2).All(t => t.First <= t.Second);
            }

            bool IsDominatedAny(int[] s)
            {
                return TupleSizeCombinations.Count(s2 => IsDominated(s, s2)) > 1;
            }

            TupleSizeCombinations = Func(Array.Empty<int>()).ToArray();
            TupleSizeCombinations = TupleSizeCombinations.Where(s => !IsDominatedAny(s)).ToArray();

            //foreach (var s in TupleSizeCombinations)
            //    Console.WriteLine(string.Join(", ", s));
        }

        public Individual<T> Generate(Random rand)
        {
            int[] sizes = rand.Choice(TupleSizeCombinations);

            GenomeTuple<T> CreateGenome(int i)
            {
                int size = i < sizes.Length ? sizes[i] : rand.Next(SizeMin, SizeMax + 1);
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
