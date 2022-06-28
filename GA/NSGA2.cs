using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using OthelloAI.Patterns;

namespace OthelloAI.GA
{
    public abstract class NSGA2
    {
        public List<Individual> ArchivePopulation { get; } = new List<Individual>();
        public List<Individual> Population { get; } = new List<Individual>();

        public abstract IEnumerable<Individual> Crossover(IEnumerable<Individual> population);

        public List<List<Individual>> NonDominatedSort(List<Individual> population)
        {
            var rankedIndividuals = new List<List<Individual>>();

            static bool Dominated(Individual ind1, Individual ind2)
            {
                return ind1.Scores.Zip(ind2.Scores, (s1, s2) => s1 < s2).All(b => b);
            }

            bool IsNonDominated(Individual ind)
            {
                return population.Count(ind2 => Dominated(ind, ind2)) == 0;
            }

            while (population.Count > 0)
            {
                List<Individual> rank0 = population.Where(IsNonDominated).ToList();
                population.RemoveAll(i => rank0.Contains(i));
                rankedIndividuals.Add(rank0);
            }

            return rankedIndividuals;
        }

        public abstract List<Individual> CrowdedTournamentSelection(IEnumerable<Individual> pop);

        public List<Individual> CreateNextArchivePopulation(List<IEnumerable<Individual>> rankedPopulation, int n)
        {
            var p = new List<Individual>();
            int rank = 0;

            while(p.Count < n)
            {
                p.AddRange(rankedPopulation[rank++]);
            }

            return CrowdedTournamentSelection(p);
        }

        public void EvaluatePopulation(List<Individual> inds)
        {
            var evaluator = new EvaluatorRandomChoice(inds.Select(i => i.CreateEvaluator()).ToArray());


        }
    }

    public class Population
    {
        List<Individual> Individuals { get; }
        int MaxParametersSize { get; }

        public Individual Crossover(Individual ind1, Individual ind2, int max, Random rand)
        {
            ulong crossover(ulong g1, ulong g2)
            {
                ulong xor = g1 ^ g2;
                ulong result = g1 & g2;

                int n_xor = Board.BitCount(xor);
                int n = 0;
                ulong b = 0;
                while(n < n_xor / 2)
                {
                    ulong b_ = 1UL << rand.Next(n_xor);
                    if ((b & b_) != 0)
                        continue;

                    n++;
                    b |= b_;
                }

                return result | System.Runtime.Intrinsics.X86.Bmi2.X64.ParallelBitDeposit(b, xor);
            }

            ulong[] genome = ind1.Genome.Zip(ind2.Genome).Select(t => crossover(t.First, t.Second)).ToArray();

            return new Individual(genome);
        }

        public void Evaluate()
        {
            var evaluator = CreateEvaluator();

            PlayerAI player = new PlayerAI(evaluator)
            {
                ParamBeg = new SearchParameters(depth: 4, stage: 0, new CutoffParameters(true, true, false)),
                ParamMid = new SearchParameters(depth: 4, stage: 16, new CutoffParameters(true, true, false)),
                ParamEnd = new SearchParameters(depth: 64, stage: 48, new CutoffParameters(true, true, false)),
                PrintInfo = false,
            };

            for (int i = 0; i < 100; i++)
            {
                var data = TrainerUtil.PlayForTrainingParallel(100, player);

                foreach (var ind in Individuals)
                {
                    float e = data.SelectMany(t => t.Item1.Select(b => ind.Trainer.Update(b, t.Item2))).Select(f => f * f).Average();
                }
            }
        }

        public IEnumerable<Evaluator> CreateEvaluators() => Individuals.Select(i => i.CreateEvaluator());

        public Evaluator CreateEvaluator() => new EvaluatorRandomChoice(CreateEvaluators().ToArray());
    }

    public class Individual
    {
        public float[] Scores { get; }
        public ulong[] Genome { get; }

        public Pattern[] Patterns { get; }
        public PatternTrainer Trainer { get; }

        public Individual(ulong[] genome)
        {
            Genome = genome;
            Patterns = genome.Select(g => new Pattern($"ga/{g}.dat", new BoardHasherMask(g), PatternType.ASYMMETRIC)).ToArray();
            Trainer = new PatternTrainer(Patterns, 0.01F);
        }

        public Evaluator CreateEvaluator() => new EvaluatorPatternBased(Patterns);

        public int ParametersSize() => Genome.Sum(g =>(int) Math.Pow(3,  Board.BitCount(g)));
    }
}
