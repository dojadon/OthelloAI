using OthelloAI.Patterns;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace OthelloAI.GA
{
    public class Score
    {
        public Individual ind;
        public float score;

        public Score(Individual ind, float score)
        {
            this.ind = ind;
            this.score = score;
        }
    }

    public class Score2D : Score
    {
        public float score2;

        public Score2D(Individual ind, float score1, float score2) : base(ind, score1)
        {
            this.score2 = score2;
        }
    }

    public class ScoreNSGA2 : Score2D
    {
        public float congestion;
        public int rank;

        public ScoreNSGA2(Individual ind, float score1, float score2, int rank, float congestion) : base(ind, score1, score2)
        {
            this.congestion = congestion;
            this.rank = rank;
        }
    }

    public class GA<T> where T : Score
    {
        private static ThreadLocal<Random> ThreadLocalRandom { get; } = new ThreadLocal<Random>(() => new Random());

        public bool IsVaryAll { get; set; }

        public int Gen { get; protected set; }

        public ISelector<T> Selecter { get; set; }
        public INaturalSelector<T> NaturalSelector { get; set; }
        public IGeneticOperator GeneticOperator { get; set; }
        public IPopulationEvaluator<T> Evaluator { get; set; }

        public static Random Random => ThreadLocalRandom.Value;

        public static void Run()
        {
            StringBuilder builder = new StringBuilder();

            string file = "ga/inds.dat";
            var log = $"ga/log_{DateTime.Now:yyyy_MM_dd_HH_mm}.csv";

            var ga = new GA<ScoreNSGA2>()
            {
                Selecter = new SelectorTournament<ScoreNSGA2>(5, s => s.congestion),
                NaturalSelector = new NaturalSelectorNSGA2(),
                GeneticOperator = new GeneticOperators((new Mutator(), 0.02F), (new TopologyCrossover(), 1F)),
                Evaluator = new PopulationEvaluatorNSGA2()
                {   
                    Evaluator1 = new PopulationEvaluatorTrainingScore(new PopulationTrainerCoLearning(1, 54, 1600, true)),
                    Evaluator2 = new PopulationEvaluatorNobeilty(),
                },
                IsVaryAll = false,
            };

            var pop = ga.Init(50, 100, 3, 8);
            // ga.Load(file);

            for (int i = 0; i < 10000; i++)
            {
                pop = ga.Step(pop, 100);
                ga.Save(file, pop.Select(s => s.ind).ToList());

                Console.WriteLine($"Gen: {i}");

                var score = pop.MinBy(ind => ind.score).First();
                (float avg, float var) = pop.Select(ind => ind.score).AverageAndVariance();

                using (StreamWriter sw = File.AppendText(log))
                {
                    // sw.WriteLine($"{ind.Score}, {avg}, {var}");
                    foreach(var s in pop.OrderBy(s => s.score))
                    {
                        sw.WriteLine($"{s.ind.Genome[0]}, {s.ind.Genome[1]}, {s.ind.Genome[2]}, {s.score}, {s.score2}, {s.rank}, {s.congestion}");
                    }
                    // sw.WriteLine(string.Join(", ", pop.Select(ind => ind.score).OrderBy(s => s)));
                }
                Console.WriteLine(score.score);
                Console.WriteLine($"{avg}, {var}");

                foreach (var b in score.ind.Genome)
                    Console.WriteLine(new Board(b, 0UL));
            }
        }

        public List<T> Init(int n_pop, int n_offspring, int n_tuple, int size_tuple)
        {
            var offspring = Enumerable.Range(0, n_offspring).Select(_ => new Individual(Enumerable.Range(0, n_tuple).Select(_ => Random.GenerateRegion(19, size_tuple)).ToArray())).ToList();
            var scores = Evaluator.Evaluate(offspring);
            return NaturalSelector.Select(scores, n_pop, Random);
        }

        public List<T> Step(List<T> pop, int n)
        {
            Console.WriteLine("Varying");
            var offspring = Vary(pop, n);
            Console.WriteLine("Evaluating");
            var scores = Evaluator.Evaluate(offspring);
            Console.WriteLine("Selecting");
            return NaturalSelector.Select(scores, pop.Count, Random);
        }

        public virtual List<Individual> Vary(List<T> list, int n)
        {
            Individual VaryInd()
            {
                return GeneticOperator.Operate(() => Selecter.Select(list, Random).ind, Random);
            }

            if (IsVaryAll)
                return Enumerable.Range(0, n).AsParallel().Select(_ => VaryInd()).ToList();

            else
                return list.Select(s => s.ind).Concat(Enumerable.Range(0, n - list.Count).AsParallel().Select(_ => VaryInd())).ToList();
        }

        public List<Individual> Load(string file)
        {
            using var reader = new BinaryReader(new FileStream(file, FileMode.Open));

            int n = reader.ReadInt32();
            var pop = new List<Individual>();

            for (int i = 0; i < n; i++)
            {
                int n_pattern = reader.ReadInt32();
                var ind = new Individual(Enumerable.Range(0, n_pattern).Select(_ => reader.ReadUInt64()).ToArray());
                pop.Add(ind);
            }

            return pop;
        }

        public void Save(string file, List<Individual> pop)
        {
            using var writer = new BinaryWriter(new FileStream(file, FileMode.Create));

            writer.Write(pop.Count);

            foreach (var ind in pop)
            {
                writer.Write(ind.Genome.Length);
                Array.ForEach(ind.Genome, writer.Write);
            }
        }
    }


    public class Individual
    {
        public ulong[] Genome { get; }

        public Pattern[] Patterns { get; }
        public PatternTrainer Trainer { get; }

        public List<float> Log { get; } = new List<float>();

        public Individual(ulong[] genome, Pattern[] patterns)
        {
            Genome = genome;
            Patterns = patterns;
            Trainer = new PatternTrainer(Patterns, 0.002F);
        }

        public Individual(ulong[] genome) : this(genome, genome.Select(g => new Pattern($"ga/{g}.dat", 10, new BoardHasherMask(g), PatternType.ASYMMETRIC)).ToArray())
        {
        }

        public Evaluator CreateEvaluator() => new EvaluatorPatternBased(Patterns);

        public static (int, int)[] ClosestPairs(ulong[] g1, ulong[] g2)
        {
            int n_gene = g1.Length;

            var pairs = Enumerable.Range(0, n_gene).SelectMany(i => Enumerable.Range(0, n_gene).Select(j => (i, j))).OrderBy(t => Board.BitCount(g1[t.i] ^ g2[t.j])).ToList();
            var added1 = new List<int>();
            var added2 = new List<int>();

            var result = new List<(int, int)>();

            foreach ((int i, int j) in pairs)
            {
                if (added1.Contains(i) || added2.Contains(j))
                    continue;

                added1.Add(i);
                added2.Add(j);

                result.Add((i, j));
            }

            return result.ToArray();
        }
    }
}
