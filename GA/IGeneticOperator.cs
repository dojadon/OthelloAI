using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using OthelloAI.Patterns;

namespace OthelloAI.GA
{
    interface IGeneticOperator
    {
        public Individual Operate(List<Individual> pop, Random rand);
    }

    public interface IMutator
    {
        public Individual Mutate(Individual individual, Random rand);
    }

    public class Mutator : IGeneticOperator
    {
        public ulong Mutate(ulong n, Random rand)
        {
            int p = rand.Next(19);
            int q = rand.Next(19);

            if (((n & (1UL << p)) >> p) != ((n & (1UL << q)) >> q))
            {
                n ^= (1UL << p);
                n ^= (1UL << q);
            }
            return n;
        }

        public Individual Mutate(Individual ind, Random rand)
        {
            return new Individual(ind.Genome.Select(g => Mutate(g, rand)).ToArray());
        }

        public Individual Operate(List<Individual> pop, Random rand)
        {
            return Mutate(rand.Choice(pop), rand);
        }
    }

    public interface ICrossover
    {
        public Individual Cx(Individual ind1, Individual ind2, Random rand);
    }

    public class Crossover : ICrossover
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

        public Individual Cx(Individual ind1, Individual ind2, Random rand)
        {
            int n_gene = ind1.Genome.Length;

            var pairs = Enumerable.Range(1, n_gene).SelectMany(i => Enumerable.Range(0, i).Select(j => (i, j))).OrderBy(t => Board.BitCount(ind1.Genome[t.i] ^ ind2.Genome[t.j]));
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
