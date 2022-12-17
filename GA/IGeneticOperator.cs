using MathNet.Numerics.Distributions;
using System;
using System.Linq;
using System.Runtime.Intrinsics.X86;

namespace OthelloAI.GA
{
    public interface IGeneticOperator1<T>
    {
        public Individual<T> Operate(Individual<T> ind, Random rand);
    }

    public class GeneticOperators1<T> : IGeneticOperator1<T>
    {
        (IGeneticOperator1<T> op, float p)[] Operators { get; }

        public GeneticOperators1(params (IGeneticOperator1<T> op, float p)[] operators)
        {
            Operators = operators;

            float total = 0;
            for (int i = 0; i < Operators.Length; i++)
            {
                Operators[i].p = total;
                total = Operators[i].p;
            }

            Operators[^1].p = 1;
        }

        public IGeneticOperator1<T> Next(Random rand)
        {
            double d = rand.NextDouble();
            return Operators.First(t => d < t.p).op;
        }

        public Individual<T> Operate(Individual<T> ind, Random rand)
        {
            return Next(rand).Operate(ind, rand);
        }
    }

    public abstract class MutantEachTuples<T> : IGeneticOperator1<T>
    {
        public abstract GenomeGroup<T> Operate(GenomeGroup<T> gene, GenomeInfo<T> info, Random rand);

        public Individual<T> Operate(Individual<T> ind, Random rand)
        {
            var g = ind.Genome.Select(a => a.Select(g => Operate(g, ind.Info, rand)).ToArray()).ToArray();
            return new Individual<T>(g, ind.Info);
        }
    }

    public class MutantBits : IGeneticOperator1<ulong>
    {
        public float Prob { get; set; }

        public MutantBits(float prob)
        {
            Prob = prob;
        }

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
            var g = ind.Genome.Select(a =>
            {
                return a.Select(g => rand.NextDouble() < Prob ? Mutant(g, rand) : g).ToArray();
            }).ToArray();

            return new Individual<ulong>(g, ind.Info);
        }
    }

    public class MutantGaussNoise : MutantEachTuples<float[]>
    {
        public float Variance { get; }

        public MutantGaussNoise(float variance)
        {
            Variance = variance;
        }

        public override GenomeGroup<float[]> Operate(GenomeGroup<float[]> gene, GenomeInfo<float[]> info, Random rand)
        {
            float[] g = gene.Genome;

            for (int i = 0; i < g.Length; i++)
            {
                g[i] += (float)Normal.Sample(rand, 0, Variance);
                g[i] = Math.Clamp(g[i], 0, 1);
            }

            return new GenomeGroup<float[]>(g, gene.Size);
        }
    }

    public class MutantRandomSize : MutantEachTuples<float[]>
    {
        public override GenomeGroup<float[]> Operate(GenomeGroup<float[]> gene, GenomeInfo<float[]> info, Random rand)
        {
            int size = rand.Next(info.SizeMin, info.SizeMax + 1);
            return new GenomeGroup<float[]>(gene.Genome, size);
        }
    }

    public class MutantRandomGenerationOneTuple<T> : IGeneticOperator1<T>
    {
        public Individual<T> Operate(Individual<T> ind, Random rand)
        {
            int index = rand.Next(ind.Info.NumTuples);

            var g = ind.Genome.Select(a => a.Select((g, i) =>
            {
                if (i != index)
                    return g;

                var gene = ind.Info.GenomeGenerator(rand);
                int size = rand.Next(ind.Info.SizeMin, ind.Info.SizeMax + 1);

                return new GenomeGroup<T>(gene, size);

            }).ToArray()).ToArray();

            return new Individual<T>(g, ind.Info);
        }
    }

    public class MutantRandomGeneration<T> : IGeneticOperator1<T>
    {
        public Individual<T> Operate(Individual<T> ind, Random rand)
        {
            return ind.Info.Generate(rand);
        }
    }

    public interface IGeneticOperator2<T>
    {
        public Individual<T> Operate(Individual<T> ind1, Individual<T> ind2, Random rand);
    }

    public class CrossoverMutant<T> : IGeneticOperator2<T>
    {
        IGeneticOperator1<T> Operator { get; }

        public CrossoverMutant(IGeneticOperator1<T> opt)
        {
            Operator = opt;
        }

        public Individual<T> Operate(Individual<T> ind1, Individual<T> ind2, Random rand)
        {
            return Operator.Operate(ind1, rand);
        }
    }

    public class CrossoverExchange<T> : IGeneticOperator2<T>
    {
        public GenomeGroup<T>[] Cx(GenomeGroup<T>[] g1, GenomeGroup<T>[] g2, Random rand)
        {
            var randoms = Enumerable.Range(0, g1.Length).Select(_ => rand.Next(2)).ToArray();

            if (randoms.All(r => r == 0))
                randoms[rand.Next(randoms.Length)] = 1;

            if (randoms.All(r => r == 1))
                randoms[rand.Next(randoms.Length)] = 0;

            return g1.ZipThree(g2, randoms, (a1, a2, r) => r > 0.5 ? a1 : a2).ToArray();
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
