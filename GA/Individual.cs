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

    public class TupleData<T>
    {
        public T Genome { get; }
        public ulong TupleBit { get; }
        public GenomeInfo<T> Info { get; }

        public int Size { get; }

        public TupleData(T genome, int size, GenomeInfo<T> info)
        {
            Genome = genome;
            Size = size;
            Info = info;

            TupleBit = info.Decoder(genome, Size);
        }

        public override bool Equals(object obj)
        {
            if (obj is TupleData<T> t)
                return Equals(t);

            return false;
        }

        public bool Equals(TupleData<T> y)
        {
            if (ReferenceEquals(this, y))
                return true;

            return Size == y.Size && TupleBit == y.TupleBit;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Size, TupleBit);
        }

        public override string ToString()
        {
            Board b = new Board(TupleBit, 0);
            string Disc(int i)
            {
                if ((TupleBit & (1ul << i)) != 0)
                    return "X";
                else
                    return "-";
            }

            string Line(int y)
            {
                return "| " + string.Join(" | ", 8.Loop(i => Disc(y * 8 + i))) + " |";
            }

            return string.Join(Environment.NewLine, Line(0), Line(1), Line(2));
        }
    }

    public class Individual<T>
    {
        public GenomeTuple<T>[][] Genome { get; }
        public TupleData<T>[][] Tuples { get; }

        public Weight Weight { get; }

        public GenomeInfo<T> Info { get; }

        public List<float> Log { get; } = new List<float>();

        public Individual(GenomeTuple<T>[][] genome, GenomeInfo<T> info)
        {
            Genome = genome;
            Info = info;

            Tuples = new TupleData<T>[info.NumStages][];
            var weights = new Weight[info.NumStages];

            for (int i = 0; i < genome.Length; i++)
            {
                var list = new List<TupleData<T>>();

                int n_weights = 0;
                foreach (var g in Genome[i])
                {
                    if (n_weights + g.NumWeights > info.MaxNumWeights)
                        continue;

                    n_weights += g.NumWeights;

                    list.Add(new TupleData<T>(g.Genome, g.Size, info));
                }
                Tuples[i] = list.OrderBy(t => t.TupleBit).ToArray();
                weights[i] = new WeightsSum(Tuples[i].Select(t => new WeightsArrayR(t.TupleBit)).ToArray());
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
                    if (Tuples[i][j].TupleBit != y.Tuples[i][j].TupleBit)
                        return false;
            }
            return true;
        }

        public int GetHashCode(IEnumerable<TupleData<T>> tuples)
        {
            return tuples.Aggregate(0, (total, next) => HashCode.Combine(total, next.TupleBit));
        }

        public override int GetHashCode()
        {
            return Tuples.Aggregate(0, (total, next) => HashCode.Combine(total, GetHashCode(next)));
        }
    }
}
