using OthelloAI.Patterns;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OthelloAI.GA
{
    public interface ISelecter
    {
        public List<Individual> Select(List<Individual> individuals, int n, Random rand);
    }

    public class SelectorTournament : ISelecter
    {
        public int TournamentSize { get; set; }

        public SelectorTournament(int tournamentSize)
        {
            TournamentSize = tournamentSize;
        }

        public List<Individual> Select(List<Individual> individuals, int n, Random rand)
        {
            return Enumerable.Range(0, n).Select(_ => Enumerable.Range(0, TournamentSize).Select(_ => rand.Choice(individuals)).MinBy(ind => ind.Score).First()).ToList();
        }
    }

    public class GA
    {
        private static ThreadLocal<Random> ThreadLocalRandom { get; } = new ThreadLocal<Random>(() => new Random());

        public List<Individual> Population { get; set; } = new List<Individual>();
        public TrainingData CurrentTrainData { get; set; } = new TrainingData();

        public int Gen { get; set; }

        public ISelecter Selecter { get; set; }
        public ICrossover Crossover { get; set; }
        public IMutator Mutator { get; set; }

        public IPopulationEvaluator Evaluator { get; set; }

        public static Random Random => ThreadLocalRandom.Value;

        public static void Run()
        {
            StringBuilder builder = new StringBuilder();

            string file = "ga/inds.dat";

            var ga = new GA()
            {
                Selecter = new SelectorTournament(10),
                Crossover = new Crossover(),
                Mutator = new Mutator(),
                Evaluator = new PopulationEvaluatorTrainingScore(new PopulationTrainerCoLearning(3, 52, 2000, true)),
            };

            ga.Init(50);
            // ga.Load(file);

            for (int i = 0; i < 1000; i++)
            {
                ga.Step(100, 0.7F, 0.0025F);
                ga.Save(file);

                Console.WriteLine($"Gen: {i}");

                var ind = ga.Population.MinBy(ind => ind.Score).First();

                using (StreamWriter sw = File.AppendText("ga/log.txt"))
                {
                    sw.WriteLine(ind.Score);
                }
                Console.WriteLine(ind.Score);

                foreach (var b in ind.Genome)
                    Console.WriteLine(new Board(b, 0UL));
            }
        }

        public void Init(int n_pop)
        {
            Population = Enumerable.Range(0, n_pop).Select(_ => new Individual(Enumerable.Range(0, 3).Select(_ => Random.GenerateRegion(19, 4)).ToArray())).ToList();
        }

        public void Step(int n, float pb_cx, float pb_mut)
        {
            var offspring = Variation(Population, n, pb_cx, pb_mut);
            Evaluator.Evaluate(offspring);
            Population = Selecter.Select(offspring, Population.Count, Random);
            Gen++;
        }

        public List<Individual> Variation(List<Individual> list, int n, float cxpb, float mutpb)
        {
            return Enumerable.Range(0, n).AsParallel().Select(_ =>
            {
                var rand = Random;
                double d = rand.NextDouble();

                if (d > mutpb)
                {
                    return Mutator.Mutate(rand.Choice(list), rand);
                }
                else if (d > cxpb + mutpb)
                {
                    return Crossover.Cx(rand.Choice(list), rand.Choice(list), rand);
                }
                else
                {
                    return rand.Choice(list);
                }
            }).ToList();
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
                Population.Add(ind);
            }
        }

        public void Save(string file)
        {
            using var writer = new BinaryWriter(new FileStream(file, FileMode.Create));

            writer.Write(Population.Count);

            foreach (var ind in Population)
            {
                writer.Write(ind.Genome.Length);
                Array.ForEach(ind.Genome, writer.Write);
            }
        }
    }


    public class Individual
    {
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
