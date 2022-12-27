using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OthelloAI.GA
{
    public class GenomeTuple<T>
    {
        public T Genome { get; }
        public int Size { get; }
        public int NumWeights { get; }

        public GenomeTuple(T genome, int size)
        {
            Genome = genome;
            Size = size;
            NumWeights = (int)Math.Pow(3, Size);
        }
    }

    public class Individual<T>
    {
        public GenomeTuple<T>[][] Genome { get; }
        public ulong[][] Tuples { get; }

        public Weight Weight { get; }

        public GenomeInfo<T> Info { get; }

        public List<float> Log { get; } = new List<float>();

        public Individual(GenomeTuple<T>[][] genome, GenomeInfo<T> info)
        {
            Genome = genome;
            Info = info;

            Tuples = new ulong[info.NumStages][];
            var weights = new Weight[info.NumStages];

            for (int i = 0; i < genome.Length; i++)
            {
                var list = new List<ulong>();

                int n_weights = 0;
                foreach (var g in Genome[i])
                {
                    if (n_weights + g.NumWeights > info.MaxNumWeights)
                        continue;

                    n_weights += g.NumWeights;

                    ulong t = info.Decoder(g.Genome, g.Size);
                    list.Add(t);
                }
                Tuples[i] = list.OrderBy(x => x).ToArray();
                weights[i] = new WeightsSum(Tuples[i].Select(t => new WeightsArrayR(t)).ToArray());
            }

            Weight = new WeightsStagebased(weights);
        }

        public float GetDepth() => Info.GetDepth(Weight, 40);

        public Evaluator CreateEvaluator() => new EvaluatorWeightsBased(Weight);

        public override bool Equals(object obj)
        {
            if (obj is Individual<T> ind)
                return Equals(ind);

            return false;
        }

        public bool Equals(Individual<T> y)
        {
            if (ReferenceEquals(this, y))
                return true;

            if (Tuples.Length != y.Tuples.Length)
                return false;

            for (int i = 0; i < Tuples.Length; i++)
            {
                if (Tuples[i].Length != y.Tuples[i].Length)
                    return false;

                for (int j = 0; j < Tuples[i].Length; j++)
                    if (Tuples[i][j] != y.Tuples[i][j])
                        return false;
            }
            return true;
        }

        public int GetHashCode(IEnumerable<ulong> tuples)
        {
            return tuples.Aggregate(0, (total, next) => HashCode.Combine(total, next));
        }

        public override int GetHashCode()
        {
            return Tuples.Aggregate(0, (total, next) => HashCode.Combine(total, GetHashCode(next)));
        }
    }
}
