using OthelloAI.Patterns;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OthelloAI.GA
{
    public class GA
    {
        public List<Individual> Population { get; set; } = new List<Individual>();
        public TrainingData CurrentData { get; set; } = new TrainingData();
        public int Gen { get; set; }

        public static void Run()
        {
            StringBuilder builder = new StringBuilder();

            string file = "ga/inds.dat";

            var rand = new Random();
            var ga = new GA();

            ga.Init(50, rand);

            for (int i = 0; i < 1000; i++)
            {
                ga.Step(150, 0.7F, 0.01F, rand);
                ga.Save(file);

                Console.WriteLine($"Gen: {i}");

                var ind = ga.Population.MinBy(ind => ind.Scores[0]).First();
                foreach(var b in ind.Genome)
                    Console.WriteLine(new Board(b, 0UL));
            }
        }

        public void Init(int n_pop, Random rand)
        {
            Population = Enumerable.Range(0, n_pop).Select(_ => new Individual(Enumerable.Range(0, 4).Select(_ => rand.GenerateRegion(19, 9)).ToArray())).ToList();
        }

        public void Step(int n, float pb_cx, float pb_mut, Random rand)
        {
            var offspring = Variation(Population, n, pb_cx, pb_mut, rand);
            Evaluate(offspring);
            Population = SelectTournament(offspring, Population.Count, 10, rand);
            Gen++;
        }

        public List<Individual> SelectTournament(List<Individual> individuals, int k, int tournsize, Random rand)
        {
            return Enumerable.Range(0, k).Select(_ => Enumerable.Range(0, tournsize).Select(_ => rand.Choice(individuals)).MinBy(ind => ind.Scores[0]).First()).ToList();
        }

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

        ulong Crossover(ulong g1, ulong g2, Random rand)
        {
            ulong xor = g1 ^ g2;

            if (xor == 0)
                return g1;

            ulong result = g1 & g2;

            int n_xor = Board.BitCount(xor);
            ulong b = rand.GenerateRegion(n_xor / 2, n_xor / 2);

            return result | System.Runtime.Intrinsics.X86.Bmi2.X64.ParallelBitDeposit(b, xor);
        }

        public Individual Crossover(Individual ind1, Individual ind2, Random rand)
        {
            ulong[] genome = ind1.Genome.Zip(ind2.Genome).Select(t => Crossover(t.First, t.Second, rand)).ToArray();

            return new Individual(genome);
        }

        public List<Individual> Variation(List<Individual> list, int n, float cxpb, float mutpb, Random rand)
        {
            return Enumerable.Range(0, n).AsParallel().Select(_ =>
            {
                double d = rand.NextDouble();

                if (d > mutpb)
                {
                    return Mutate(rand.Choice(list), rand);
                }
                else if (d > cxpb + mutpb)
                {
                    return Crossover(rand.Choice(list), rand.Choice(list), rand);
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
                ParamBeg = new SearchParameters(depth: 3, stage: 0, new CutoffParameters(true, true, false)),
                ParamMid = new SearchParameters(depth: 3, stage: 16, new CutoffParameters(true, true, false)),
                ParamEnd = new SearchParameters(depth: 64, stage: 48, new CutoffParameters(true, true, false)),
                PrintInfo = false,
            };

            if (Gen % 10 == 0)
            {
                CurrentData.Clear();

                for (int i = 0; i < 100; i++)
                {
                    var data = TrainerUtil.PlayForTrainingParallel(50, player);
                    CurrentData.AddRange(data);

                    Parallel.ForEach(individuals, ind =>
                    {
                        float e = data.Select(t => ind.Trainer.Update(t.board, t.result)).Select(f => f * f).Average();
                        ind.Scores[0] = e;
                    });
                    Console.WriteLine(individuals.Average(ind => ind.Scores[0]));
                }
            }
            else
            {
                Parallel.ForEach(individuals, ind =>
                {
                    float e = CurrentData.Select(t => ind.Trainer.Update(t.board, t.result)).Select(f => f * f).Average();
                    ind.Scores[0] = e;
                });
                Console.WriteLine(individuals.Average(ind => ind.Scores[0]));
            }
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
        public float[] Scores { get; } = new float[1];
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

        public int ParametersSize() => Genome.Sum(g => (int)Math.Pow(3, Board.BitCount(g)));
    }
}
