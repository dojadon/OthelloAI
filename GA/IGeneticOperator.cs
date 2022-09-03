using OthelloAI.Patterns;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.X86;

namespace OthelloAI.GA
{
    public class GeneticOperators<T> : IGeneticOperator<T>
    {
        List<(IGeneticOperator<T>, float prob)> Operators { get; }

        public GeneticOperators(params (IGeneticOperator<T>, float)[] operators)
        {
            Operators = operators.ToList();
        }

        public Individual<T> Operate(Func<Individual<T>> selector, Random rand)
        {
            double d = rand.NextDouble();

            float sum = 0;
            foreach ((var opt, float p) in Operators)
            {
                sum += p;

                if (d < sum)
                    return opt.Operate(selector, rand);
            }

            return selector();
        }
    }

    public interface IGeneticOperator<T>
    {
        public Individual<T> Operate(Func<Individual<T>> selector, Random rand);
    }

    public class Mutator : IGeneticOperator<ulong>
    {
        public static ulong mask = (1UL << 20) - 1;

        public static ulong Mutate(ulong g, Random rand)
        {
            int n = Board.BitCount(g);

            ulong p = Bmi2.X64.ParallelBitDeposit(1UL << rand.Next(n), g);
            ulong q = Bmi2.X64.ParallelBitDeposit(1UL << rand.Next(19 - n), ~g);

            return g ^ p ^ q;
        }

        public Individual<ulong> Mutate(Individual<ulong> ind, Random rand)
        {
            float prob = 1F / ind.Genome.Length;

            return new Individual<ulong>(ind.Genome.Select(g =>
            {
                if (rand.NextDouble() < prob)
                    return Mutate(g, rand);
                else
                    return g;

            }).ToArray(), ind.Decoder);
        }

        public Individual<ulong> Operate(Func<Individual<ulong>> selector, Random rand)
        {
            return Mutate(selector(), rand);
        }
    }

    public abstract class Crossover<T> : IGeneticOperator<T>
    {
        public abstract Individual<T> Cx(Individual<T> ind1, Individual<T> ind2, Random rand);

        public Individual<T> Operate(Func<Individual<T>> selector, Random rand)
        {
            return Cx(selector(), selector(), rand);
        }
    }

    public abstract class CrossoverSeparately<T> : Crossover<T>
    {
        public abstract T Cx(T ind1,T ind2, Random rand);

        public override Individual<T> Cx(Individual<T> ind1, Individual<T> ind2, Random rand)
        {
            return new Individual<T>(ind1.Genome.Zip(ind2.Genome, (g1, g2) => Cx(g1, g2, rand)).ToArray(), ind1.Decoder);
        }
    }


    public class BiasedCrossover<T> : CrossoverSeparately<T>
    {
        public float Bias { get; }

        public override T Cx(T g1, T g2, Random rand)
        {
            return rand.NextDouble() < Bias ? g1 : g2;
        }
    }

    public class TopologyCrossover : Crossover<ulong>
    {
        public static ulong CrossoverGenome(ulong g1, ulong g2, Random rand)
        {
            ulong xor = g1 ^ g2;

            if (xor == 0)
                return g1;

            ulong result = g1 & g2;

            int n_xor = Board.BitCount(xor);
            ulong b = rand.GenerateRegion(n_xor, n_xor / 2);

            return result | Bmi2.X64.ParallelBitDeposit(b, xor);
        }

        public override Individual<ulong> Cx(Individual<ulong> ind1, Individual<ulong> ind2, Random rand)
        {
            var next_gene = new List<ulong>();

            foreach ((int i, int j) in Individual<ulong>.ClosestPairs(ind1.Genome, ind2.Genome))
            {
                ulong gene = CrossoverGenome(ind1.Genome[i], ind2.Genome[j], rand);
                // Pattern.Test(ind1.Patterns[i].StageBasedEvaluations, p.StageBasedEvaluations, ind1.Genome[i], gene);
                // Pattern.Test(ind1.Patterns[j].StageBasedEvaluations, p.StageBasedEvaluations, ind1.Genome[j], gene);
                next_gene.Add(gene);
            }

            return new Individual<ulong>(next_gene.ToArray(), ind1.Decoder);
        }
    }
}
