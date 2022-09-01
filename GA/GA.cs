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

        public Score2D(Individual ind, float score1, float  score2) : base(ind, score1)
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

        public List<T> Population { get; set; }

        public int Gen { get; protected set; }

        public ISelector<T> Selecter { get; set; }
        public INaturalSelector<T> NaturalSelecter { get; set; }
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
                NaturalSelecter = new NaturalSelectorNSGA2(),
                GeneticOperator = new GeneticOperators((new Mutator(), 0.02F), (new TopologyCrossover(), 0.7F)),
                // Evaluator = new PopulationEvaluatorTrainingScore(new PopulationTrainerCoLearning(1, 54, 1600, true)),
                Evaluator = new PopulationEvaluatorNSGA2(),
            };

            ga.Init(50, 3, 8);
            // ga.Load(file);

            for (int i = 0; i < 10000; i++)
            {
                ga.Step(100);
                ga.Save(file);

                Console.WriteLine($"Gen: {i}");

                var score = ga.Population.MinBy(ind => ind.score).First();
                (float avg, float var) = ga.Population.Select(ind => ind.score).AverageAndVariance();

                using (StreamWriter sw = File.AppendText(log))
                {
                    // sw.WriteLine($"{ind.Score}, {avg}, {var}");
                    sw.WriteLine(string.Join(", ", ga.Population.Select(ind => ind.score).OrderBy(s => s)));
                }
                Console.WriteLine(score.score);
                Console.WriteLine($"{avg}, {var}");

                foreach (var b in score.ind.Genome)
                    Console.WriteLine(new Board(b, 0UL));
            }
        }

        public void Init(int n_pop, int n_tuple, int size_tuple)
        {
            var pop = Enumerable.Range(0, n_pop).Select(_ => new Individual(Enumerable.Range(0, n_tuple).Select(_ => Random.GenerateRegion(19, size_tuple)).ToArray())).ToList();
        }

        public void Step(int n)
        {
            var offspring = Variation(Population, n);
            var scores = Evaluator.Evaluate(offspring);
            Population = NaturalSelecter.Select(scores, Population.Count, Random);
            Gen++;
        }

        public virtual List<Individual> Variation(List<T> list, int n)
        {
            Individual VariationInd()
            {
                return GeneticOperator.Operate(() => Selecter.Select(list, Random).ind, Random);
            }

            return Enumerable.Range(0, n).AsParallel().Select(_ => VariationInd()).ToList();
        }

        public void Load(string file)
        {
            using var reader = new BinaryReader(new FileStream(file, FileMode.Open));

            int n = reader.ReadInt32();

            Population.Clear();

            for (int i = 0; i < n; i++)
            {
                int n_pattern = reader.ReadInt32();
                var ind = new Individual(Enumerable.Range(0, n_pattern).Select(_ => reader.ReadUInt64()).ToArray());
                // Population.Add(ind);
            }
        }

        public void Save(string file)
        {
            using var writer = new BinaryWriter(new FileStream(file, FileMode.Create));

            writer.Write(Population.Count);

            foreach (var ind in Population)
            {
                // writer.Write(ind.Genome.Length);
                // Array.ForEach(ind.Genome, writer.Write);
            }
        }
    }


    public class Individual
    {
        public int Congestion { get; set; }
        public int Nobelity { get; set; }
        public float Score { get; set; }

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
    }
}
