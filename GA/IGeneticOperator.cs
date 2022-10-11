using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.X86;

namespace OthelloAI.GA
{
    public class GeneticOperators<T, U> where U : Score<T>
    {
        public ISelector<T, U> Selector { get; set; }
        public IGeneticOperator1<T> Operator1 { get; set; }
        public IGeneticOperator2<T> Operator2 { get; set; }

        public float ProbCx { get; set; }

        public Individual<T> Operate(List<U> pop, Random rand)
        {
            if (rand.NextDouble() > ProbCx)
            {
                var pair = Selector.SelectPair(pop, rand);
                return Operator2.Operate(pair.Item1.ind, pair.Item2.ind, rand);
            }
            else
            {
                return Operator1.Operate(Selector.Select(pop, rand).ind, rand);
            }
        }
    }

    public interface IGeneticOperator1<T>
    {
        public Individual<T> Operate(Individual<T> ind, Random rand);
    }

    public interface IGeneticOperator2<T>
    {
        public Individual<T> Operate(Individual<T> ind1, Individual<T> ind2, Random rand);
    }

    public class CrossoverEliteBiased : IGeneticOperator2<float[]>
    {
        public float Bias { get; set; }

        public CrossoverEliteBiased(float bias)
        {
            Bias = bias;
        }

        public U Cx<U>(U a1, U a2, Random rand)
        {
            return rand.NextDouble() < Bias ? a1 : a2;
        }

        public float[] Cx(float[] g1, float[] g2, Random rand)
        {
            return g1.Zip(g2, (a1, a2) => Cx(a1, a2, rand)).ToArray();
        }

        public GenomeGroup<float[]> Cx(GenomeGroup<float[]> g1, GenomeGroup<float[]> g2, Random rand)
        {
            var gene = g1.Genome.Zip(g2.Genome, (a1, a2) => Cx(a1, a2, rand)).ToArray();
            int size = Cx(g1.Size, g2.Size, rand);

            return new GenomeGroup<float[]>(gene, size);
        }

        public Individual<float[]> Operate(Individual<float[]> ind1, Individual<float[]> ind2, Random rand)
        {
            var gene = ind1.Genome.Zip(ind2.Genome, (g1, g2) => Cx(g1, g2, rand)).ToArray();

            return new Individual<float[]>(gene, ind1.Info);
        }
    }
}
