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
                Pattern.Test(ind1.Patterns[i].StageBasedEvaluations, p.StageBasedEvaluations, ind1.Genome[i], gene);
                Pattern.Test(ind1.Patterns[j].StageBasedEvaluations, p.StageBasedEvaluations, ind1.Genome[j], gene);

                next_gene.Add(gene);
                patterns.Add(p);
            }

            return new Individual(next_gene.ToArray(), patterns.ToArray());
        }
    }

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
            return Enumerable.Range(0, n).Select(_ => Enumerable.Range(0, TournamentSize).Select(_ => rand.Choice(individuals)).MinBy(ind => ind.Scores[0]).First()).ToList();
        }
    }

    public interface IMutator
    {
        public Individual Mutate(Individual individual, Random rand);
    }

    public class Mutater : IMutator
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

        public IPopulationEvaluator Evaluator {get; set;}

        public static Random Random => ThreadLocalRandom.Value;

        public static void Run()
        {
            StringBuilder builder = new StringBuilder();

            string file = "ga/inds.dat";

            var ga = new GA()
            {
                Selecter = new SelectorTournament(10),
                Crossover = new Crossover(),
                Mutator = new Mutater(),
            };

            ga.Init(50);
            // ga.Load(file);

            for (int i = 0; i < 1000; i++)
            {
                ga.Step(100, 0.7F, 0.0025F);
                ga.Save(file);

                Console.WriteLine($"Gen: {i}");

                var ind = ga.Population.MinBy(ind => ind.Scores[0]).First();

                using (StreamWriter sw = File.AppendText("ga/log.txt"))
                {
                    sw.WriteLine(ind.Scores[0]);
                }
                Console.WriteLine(ind.Scores[0]);

                foreach (var b in ind.Genome)
                    Console.WriteLine(new Board(b, 0UL));
            }
        }

        public void Init(int n_pop)
        {
            Population = Enumerable.Range(0, n_pop).Select(_ => new Individual(Enumerable.Range(0, 4).Select(_ => Random.GenerateRegion(19, 8)).ToArray())).ToList();
        }

        public void Step(int n, float pb_cx, float pb_mut)
        {
            var offspring = Variation(Population, n, pb_cx, pb_mut);
            Evaluate(offspring);
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

        public void Evaluate(List<Individual> individuals)
        {
            var evaluator = new EvaluatorRandomChoice(individuals.Select(i => i.CreateEvaluator()).ToArray());

            PlayerAI player = new PlayerAI(evaluator)
            {
                ParamBeg = new SearchParametersSimple(depth: 7, stage: 0, new CutoffParameters(true, true, false)),
                ParamMid = new SearchParametersSimple(depth: 7, stage: 16, new CutoffParameters(true, true, false)),
                ParamEnd = new SearchParametersSimple(depth: 64, stage: 48, new CutoffParameters(true, true, false)),
                PrintInfo = false,
            };

            for (int i = 0; i < 500; i++)
            {
                var data = TrainerUtil.PlayForTrainingParallel(10, player);

                Parallel.ForEach(individuals, ind =>
                {
                    float e = data.Select(t => ind.Trainer.Update(t.board, t.result)).Select(f => f * f).Average();
                    ind.Scores[0] = e;
                });
                Console.WriteLine(individuals.Average(ind => ind.Scores[0]));
            }

            var test = TrainerUtil.PlayForTrainingParallel(50, player);
            Parallel.ForEach(individuals, ind =>
            {
                float e = test.Select(t => ind.Trainer.Update(t.board, t.result)).Select(f => f * f).Average();
                ind.Scores[0] = e;
            });
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

    public interface IPopulationEvaluator
    {
        public List<(Individual, float)> Evaluate(List<Individual> pop);
    }

    public class PopulationEvaluatorSelfPlay : IPopulationEvaluator
    {
        public int 

        public List<(Individual, float)> Evaluate(List<Individual> pop)
        {

        }
    }


    public class Individual
    {
        public float[] Scores { get; } = new float[1];
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

        public int ParametersSize() => Genome.Sum(g => (int)Math.Pow(3, Board.BitCount(g)));
    }
}
