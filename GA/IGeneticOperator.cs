using OthelloAI.Patterns;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.X86;

namespace OthelloAI.GA
{
    public class GeneticOperators<T> : IGeneticOperator<T> where T : IndividualBase
    {
        List<(IGeneticOperator<T>, float prob)> Operators { get; }

        public GeneticOperators(params (IGeneticOperator<T>, float)[] operators)
        {
            Operators = operators.ToList();
        }

        public T Operate(Func<T> selector, Random rand)
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

    public interface IGeneticOperator<T> where T : IndividualBase
    {
        public T Operate(Func<T> selector, Random rand);
    }

    public class Mutator : IGeneticOperator<Individual>
    {
        public static ulong mask = (1UL << 20) - 1;

        public static ulong Mutate(ulong g, Random rand)
        {
            int n = Board.BitCount(g);

            ulong p = Bmi2.X64.ParallelBitDeposit(1UL << rand.Next(n), g);
            ulong q = Bmi2.X64.ParallelBitDeposit(1UL << rand.Next(19 - n), ~g);

            return g ^ p ^ q;
        }

        public Individual Mutate(Individual ind, Random rand)
        {
            float prob = 1F / ind.Genome.Length;

            return new Individual(ind.Genome.Select(g =>
            {
                if (rand.NextDouble() < prob)
                    return Mutate(g, rand);
                else
                    return g;

            }).ToArray());
        }

        public Individual Operate(Func<Individual> selector, Random rand)
        {
            return Mutate(selector(), rand);
        }
    }

    public abstract class Crossover<T> : IGeneticOperator<T> where T : IndividualBase
    {
        public abstract T Cx(T ind1, T ind2, Random rand);

        public T Operate(Func<T> selector, Random rand)
        {
            return Cx(selector(), selector(), rand);
        }
    }

    public class BiasedCrossover : Crossover<IndividualRK>
    {
        public float Bias { get; }

        public float[] Cx(float[] g1, float[] g2, Random rand)
        {
            return g1.Zip(g2, (a1, a2) => rand.NextDouble() < Bias ? a1 : a2).ToArray();
        }

        public override IndividualRK Cx(IndividualRK ind1, IndividualRK ind2, Random rand)
        {
            float[][] gen = ind1.Genome.Zip(ind2.Genome, (g1, g2) => Cx(g1, g2, rand)).ToArray();
            return new IndividualRK(gen);
        }
    }

    public class TopologyCrossover : Crossover<Individual>
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

        public override Individual Cx(Individual ind1, Individual ind2, Random rand)
        {
            var next_gene = new List<ulong>();

            foreach ((int i, int j) in Individual.ClosestPairs(ind1.Genome, ind2.Genome))
            {
                ulong gene = CrossoverGenome(ind1.Genome[i], ind2.Genome[j], rand);
                // Pattern.Test(ind1.Patterns[i].StageBasedEvaluations, p.StageBasedEvaluations, ind1.Genome[i], gene);
                // Pattern.Test(ind1.Patterns[j].StageBasedEvaluations, p.StageBasedEvaluations, ind1.Genome[j], gene);
                next_gene.Add(gene);
            }

            return new Individual(next_gene.ToArray());
        }
    }
}
