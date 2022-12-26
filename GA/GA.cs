using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace OthelloAI.GA
{
    public class Score<T>
    {
        public Individual<T> ind;
        public float score;

        public Score(Individual<T> ind, float score)
        {
            this.ind = ind;
            this.score = score;
        }
    }

    public class Score2D<T> : Score<T>
    {
        public float score2;

        public Score2D(Individual<T> ind, float score1, float score2) : base(ind, score1)
        {
            this.score2 = score2;
        }
    }

    public class ScoreNSGA2<T> : Score2D<T>
    {
        public float congestion;
        public int rank;

        public ScoreNSGA2(Individual<T> ind, float score1, float score2, int rank, float congestion) : base(ind, score1, score2)
        {
            this.congestion = congestion;
            this.rank = rank;
        }
    }

    public class GATest
    {
        public static void TestES()
        {
            var info = new GenomeInfo<ulong>()
            {
                NumStages = 1,
                NumTuples = 9,
                SizeMin = 7,
                SizeMax = 7,
                MaxNumWeights = (int)Math.Pow(3, 9),
                GenomeGenerator = rand => rand.GenerateRegion(19, 7),
                Decoder = (g, _) => g,
            };

            var ga = new GA<ulong, Score<ulong>>()
            {
                Info = info,
                Evaluator = new PopulationEvaluatorRandomTournament<ulong>(new PopulationTrainerCoLearning(1, 54, 160, 16000, -1), 2, 54, 100 * 400)
                {
                    GetDepthFraction = (_, _, _) => (1, 1)
                },
                // Evaluator = new PopulationEvaluatorTrainingScore<float[]>(new PopulationTrainerCoLearning(1, 54, 3200, true)),
                Variator = new VariatorES<ulong>()
                {
                    Mu = 10,
                    LambdaM = 80,
                    LambdaCX = 10,
                    Mutant = new MutantBits(0.25F),
                    Crossover = new CrossoverExchange<ulong>(),
                },
            };

            var log = $"G:/マイドライブ/Lab/test/ga/log_es_7x9_{DateTime.Now:yyyy_MM_dd_HH_mm}.csv";
            using StreamWriter sw = File.AppendText(log);

            // ga.Run(ga.IO.Load("ga/ind.dat"), (n_gen, time, pop) =>
            ga.Run(ga.Init(100), (n_gen, time, pop) =>
            {
                var score = pop.MinBy(ind => ind.score).First();

                foreach (var s in pop.OrderBy(s => s.score))
                {
                    sw.Write(s.score + ",");

                    foreach (var t in s.ind.Tuples)
                    {
                        sw.Write(",," + string.Join(", ", t.Select(t => t.TupleBit)));
                    }
                    sw.WriteLine();
                }

                foreach (var t in score.ind.Tuples)
                {
                    ulong[] tuples = t.Select(t => t.TupleBit).ToArray();

                    for (int i = 0; i < tuples.Length / 2.0F; i++)
                    {
                        var b1 = tuples[i * 2];

                        if (i * 2 + 1 < tuples.Length)
                        {
                            var b2 = tuples[i * 2 + 1];
                            Console.WriteLine(new Board(b1, Board.HorizontalMirror(b2)));
                        }
                        else
                        {
                            Console.WriteLine(new Board(b1, 0));
                        }
                    }
                    Console.WriteLine();
                }

                Console.WriteLine($"Gen : {n_gen}, {time}");
                Console.WriteLine(string.Join(", ", score.ind.Tuples.Select(t => $"({string.Join(", ", t.Select(t => t.Size))})")));

                sw.Flush();
            });
        }

        public static void TestBRKGA()
        {
            static ulong Decode(float[] keys, int size)
            {
                var indices = keys.Select((k, i) => (k, i)).OrderBy(t => t.k).Select(t => t.i).Take(size);

                ulong g = 0;
                foreach (var i in indices)
                    g |= 1UL << i;
                return g;
            }

            var info = new GenomeInfo<float[]>()
            {
                NumStages = 1,
                NumTuples = 12,
                SizeMin = 7,
                SizeMax = 9,
                MaxNumWeights = (int)Math.Pow(3, 10) * 1,
                MinDepth = 1,
                GenomeGenerator = rand => Enumerable.Range(0, 19).Select(_ => (float)rand.NextDouble()).ToArray(),
                Decoder = Decode,
            };

            int n_dimes = 8;
            int dime_size = 100;

            static bool p(TrainingDataElement t) => 38 <= t.board.n_stone && t.board.n_stone <= 40;
            var data = Enumerable.Range(2001, 15).SelectMany(i => WthorRecordReader.Read($"WTH/WTH_{i}.wtb").SelectMany(x => x).Where(p)).OrderBy(i => Guid.NewGuid()).ToArray();
            var data_splited = ArrayUtil.Divide(data, n_dimes - 1).Select(a => new TrainingData(a)).ToArray();

            IEnumerable<TrainingDataElement> GetTrainingData(int index)
            {
                return data_splited.Length.Loop().Where(i => i != index).SelectMany(i => data_splited[i]);
            }

            var ga = new GA<float[], Score<float[]>>()
            {
                Info = info,
                // Evaluator = new PopulationEvaluatorTrainingScorebySelfMatch<float[]>(new PopulationTrainerCoLearning(4, 48, 9600, 9600, -1)),
                //Evaluator = new PopulationEvaluatorDistributed<float[], Score<float[]>>()
                //{
                //    Evaluators = (n_dimes - 1).Loop(i => new PopulationEvaluatorTrainingScore<float[]>(GetTrainingData(i), data_splited[i]))
                //        .ConcatOne<IPopulationEvaluator<float[], Score<float[]>>>(new PopulationEvaluatorTrainingScoreKFold<float[]>(data_splited)).ToArray(),
                //},
                Evaluator = new PopulationEvaluatorTrainingScoreKFoldWithVariableDepth<float[]>(data_splited),
                //Variator = new VariatorEliteArchive<float[]>()
                //{
                //    NumElites = 20 * pop_size / 100,
                //    Groups = new VariationGroup<float[]>[] {
                //        new VariationGroupOperation<float[]>(new CrossoverEliteBiased(0.7F), 60 *  pop_size / 100, 8),
                //        new VariationGroupRandom<float[]>(20 * pop_size / 100, 0),
                //    },
                //},
                Variator = new VariatorEliteArchiveDistributed<float[]>()
                {
                    NumDime = n_dimes,
                    MigrationRate = 50,
                    NumElites = 20,
                    NumElitesMigration = 2,
                    Groups = new VariationGroup<float[]>[] {
                        new VariationGroupOperation<float[]>(new CrossoverEliteBiased(0.7F), 65, 8),
                        new VariationGroupRandom<float[]>(15, 0),
                    },
                },
            };

            string directory = $"ga/brkga_{DateTime.Now:yyyy_MM_dd_HH_mm}";
            Directory.CreateDirectory(directory);

            var log_tuple = $"{directory}/tuple.csv";
            using StreamWriter sw_tuple = File.AppendText(log_tuple);

            var log_gene = $"{directory}/gene.csv";
            using StreamWriter sw_gene = File.AppendText(log_gene);

            var log_score = $"{directory}/score.csv";
            using StreamWriter sw_score = File.AppendText(log_score);

            ga.Run(ga.Init(n_dimes * dime_size), (gen, time, pop) =>
            {
                foreach (var t in pop.Select((s, i) => (s, i)).OrderBy(t => t.i / 100).ThenBy(t => t.s.score))
                {
                    var s = t.s;
                    int id = t.i / 100;
                    int rank = t.i % 100;

                    sw_tuple.WriteLine($"{gen},{id}," + string.Join(",,", s.ind.Tuples.Select(t => string.Join(", ", t.Select(t => t.TupleBit)))));
                    sw_gene.WriteLine($"{gen},{id}," + string.Join(",", s.ind.Tuples[0].Select(t => string.Join(",", t.Genome))));
                }

                //var top_scores = Enumerable.Range(0, pop.Count / 100).AsParallel().Select(i =>
                //{
                //    var s = pop[i * 100];

                //    return data_splited.Length.Loop().AsParallel().Select(j =>
                //    {
                //        var trainer = new Trainer(s.ind.Weight.Copy(), 0.001F);

                //        var train_data = Enumerable.Range(0, data_splited.Length).Where(x => x != j).SelectMany(i => data_splited[i]);
                //        var valid_data = data_splited[j];

                //        return trainer.TrainAndTest(train_data, valid_data);
                //    }).Average();
                //}).ToArray();

                var top_scores = Enumerable.Range(0, pop.Count / 100).Select(i => pop[i * 100].score).ToArray();
                sw_score.WriteLine(string.Join(",", top_scores));

                var score = pop.Take(100).MinBy(ind => ind.score).First();

                foreach (var t in score.ind.Tuples)
                {
                    foreach (var tt in t)
                    {
                        Console.WriteLine(tt);
                        Console.WriteLine();
                    }
                }

                Console.WriteLine($"Gen {gen}, {time:f1}s, score {score.score}");
                Console.WriteLine(string.Join(",", score.ind.Tuples.Select(t => $"({string.Join(", ", t.Select(t => t.Size))})")));

                sw_tuple.Flush();
                sw_gene.Flush();
                sw_score.Flush();
            });
        }
    }

    public class GA<T, U> where U : Score<T>
    {
        private static ThreadLocal<Random> ThreadLocalRandom { get; } = new ThreadLocal<Random>(() => new Random());

        public GenomeInfo<T> Info { get; set; }

        public IPopulationEvaluator<T, U> Evaluator { get; set; }
        public IVariator<T, U> Variator { get; set; }

        public static Random Random => ThreadLocalRandom.Value;

        public static string ConcatHorizontal(string s1, string s2, int margin)
        {
            var a1 = s1.Split(Environment.NewLine);
            var a2 = s2.Split(Environment.NewLine);

            int max = a1.Concat(a2).Max(s => s.Length);
            int len = max + margin;

            return a1.Zip(a2, (b1, b2) => b1.PadRight(len) + b2).Aggregate((b1, b2) => b1 + Environment.NewLine + b2);
        }

        public void Run(List<Individual<T>> pop, Action<int, float, List<U>> logger)
        {
            var timer = new System.Diagnostics.Stopwatch();

            for (int i = 0; i < 10000; i++)
            {
                timer.Restart();

                var scores = Evaluator.Evaluate(pop);

                timer.Stop();
                float time = (float)timer.ElapsedTicks / System.Diagnostics.Stopwatch.Frequency;
                logger(i, time, scores);

                pop = Variator.Vary(scores, i, Random);
            }
        }

        public List<Individual<T>> Init(int n_pop)
        {
            Info.Init();
            return Enumerable.Range(0, n_pop).Select(_ => Info.Generate(Random)).ToList();
        }
    }
}
