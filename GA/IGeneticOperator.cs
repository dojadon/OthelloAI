using OthelloAI.Patterns;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OthelloAI.GA
{
    public class GeneticOperators : IGeneticOperator
    {
        List<(IGeneticOperator, float prob)> Operators { get; }

        public GeneticOperators(params (IGeneticOperator, float)[] operators)
        {
            Operators = operators.ToList();
        }

        public Individual Operate(Func<Individual> selector, Random rand)
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

    public interface IGeneticOperator
    {
        public Individual Operate(Func<Individual> selector, Random rand);
    }

    public class Mutator : IGeneticOperator
    {
        public ulong Mutate(ulong n, Random rand)
        {
            ulong p = 1UL << rand.Next(19);
            ulong q = 1UL << rand.Next(19);

            return n ^ p ^ q;
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

    public abstract class Crossover : IGeneticOperator
    {
        public abstract Individual Cx(Individual ind1, Individual ind2, Random rand);

        public Individual Operate(Func<Individual> selector, Random rand)
        {
            return Cx(selector(), selector(), rand);
        }
    }

    public class TopologyCrossover : Crossover
    {
        ulong CrossoverGenome(ulong g1, ulong g2, Random rand)
        {
            ulong xor = g1 ^ g2;

            if (xor == 0)
                return g1;

            ulong result = g1 & g2;

            int n_xor = Board.BitCount(xor);
            ulong b = rand.GenerateRegion(n_xor / 2, n_xor / 2);

            return result | System.Runtime.Intrinsics.X86.Bmi2.X64.ParallelBitDeposit(b, xor);
        }

        public override Individual Cx(Individual ind1, Individual ind2, Random rand)
        {
            int n_gene = ind1.Genome.Length;

            var pairs = Enumerable.Range(0, n_gene - 1).SelectMany(i => Enumerable.Range(i + 1, n_gene - i - 1).Select(j => (i, j))).OrderBy(t => Board.BitCount(ind1.Genome[t.i] ^ ind2.Genome[t.j]));
            var added = new List<int>();
            var next_gene = new List<ulong>();
            var patterns = new List<Pattern>();

            foreach ((int i, int j) in pairs)
            {
                if (added.Contains(i) || added.Contains(j))
                    continue;

                ulong gene = CrossoverGenome(ind1.Genome[i], ind2.Genome[j], rand);
                var p = new Pattern($"ga/{gene}.dat", 10, new BoardHasherMask(gene), PatternType.ASYMMETRIC);
                // Pattern.Test(ind1.Patterns[i].StageBasedEvaluations, p.StageBasedEvaluations, ind1.Genome[i], gene);
                // Pattern.Test(ind1.Patterns[j].StageBasedEvaluations, p.StageBasedEvaluations, ind1.Genome[j], gene);

                next_gene.Add(gene);
                patterns.Add(p);
            }

            return new Individual(next_gene.ToArray(), patterns.ToArray());
        }
    }
}
