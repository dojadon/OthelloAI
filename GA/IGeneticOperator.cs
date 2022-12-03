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

    public class MutantBits : IGeneticOperator1<ulong>
    {
        public static ulong Mutant(ulong g, ulong mask, Random rand)
        {
            int n1 = Board.BitCount(g);
            int n2 = Board.BitCount(mask) - n1;
            ulong dist = g;

            dist ^= Bmi2.X64.ParallelBitDeposit(1UL << rand.Next(n1), g);
            dist |= Bmi2.X64.ParallelBitDeposit(1UL << rand.Next(n2), mask ^ g);

            return dist;
        }

        public static GenomeGroup<ulong> Mutant(GenomeGroup<ulong> gene, Random rand)
        {
            ulong mask = (2UL << gene.Size) - 1;
            return new GenomeGroup<ulong>(Mutant(gene.Genome, mask, rand), gene.Size);
        }

        public Individual<ulong> Operate(Individual<ulong> ind, Random rand)
        {
            var g = ind.Genome.Select(a => a.Select(g => Mutant(g, rand)).ToArray()).ToArray();
            return new Individual<ulong>(g, ind.Info);
        }
    }

    public class MutantRK : IGeneticOperator1<float[]>
    {
        public float ProbSwapping { get; }
        public float ProbChangingSize { get; }

        public MutantRK(float probSwapping, float probChangingSize)
        {
            ProbSwapping = probSwapping;
            ProbChangingSize = probChangingSize;
        }

        public GenomeGroup<float[]> Operate(GenomeGroup<float[]> gene, GenomeInfo<float[]> info, Random rand)
        {
            int size = gene.Size;
            float[] g = gene.Genome;

            if (rand.NextDouble() < ProbChangingSize)
            {
                size = rand.Next(info.SizeMin, info.SizeMax + 1);
            }

            if (rand.NextDouble() < ProbSwapping)
            {
                int i1 = rand.Next(0, g.Length);
                int i2 = rand.Next(0, g.Length);

                (g[i2], g[i1]) = (g[i1], g[i2]);
            }

            return new GenomeGroup<float[]>(g, size);
        }

        public Individual<float[]> Operate(Individual<float[]> ind, Random rand)
        {
            var g = ind.Genome.Select(a => a.Select(g => Operate(g, ind.Info, rand)).ToArray()).ToArray();
            return new Individual<float[]>(g, ind.Info);
        }
    }

    public interface IGeneticOperator2<T>
    {
        public Individual<T> Operate(Individual<T> ind1, Individual<T> ind2, Random rand);
    }

    public class CrossoverExchange<T> : IGeneticOperator2<T>
    {
        public GenomeGroup<T>[] Cx(GenomeGroup<T>[] g1, GenomeGroup<T>[] g2, Random rand)
        {
            return g1.Zip(g2, (a1, a2) => rand.NextDouble() > 0.5 ? a1 : a2).ToArray();
        }

        public Individual<T> Operate(Individual<T> ind1, Individual<T> ind2, Random rand)
        {
            var gene = ind1.Genome.Zip(ind2.Genome, (g1, g2) => Cx(g1, g2, rand)).ToArray();
            return new Individual<T>(gene, ind1.Info);
        }
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

        public float[] CxArray(float[] g1, float[] g2, Random rand)
        {
            return g1.Zip(g2, (a1, a2) => Cx(a1, a2, rand)).ToArray();
        }

        public GenomeGroup<float[]> Cx2(GenomeGroup<float[]> g1, GenomeGroup<float[]> g2, Random rand)
        {
            var gene = CxArray(g1.Genome, g2.Genome, rand);
            int size = Cx(g1.Size, g2.Size, rand);

            return new GenomeGroup<float[]>(gene, size);
        }

        public GenomeGroup<float[]>[] Cx1(GenomeGroup<float[]>[] g1, GenomeGroup<float[]>[] g2, Random rand)
        {
            return g1.Zip(g2, (g11, g22) => Cx2(g11, g22, rand)).ToArray();
        }

        public Individual<float[]> Operate(Individual<float[]> ind1, Individual<float[]> ind2, Random rand)
        {
            var gene = ind1.Genome.Zip(ind2.Genome, (g1, g2) => Cx1(g1, g2, rand)).ToArray();

            return new Individual<float[]>(gene, ind1.Info);
        }
    }
}
