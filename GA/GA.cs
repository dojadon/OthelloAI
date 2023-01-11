using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
                //Evaluator = new PopulationEvaluatorRandomTournament<ulong>(new PopulationTrainerCoLearning(1, 54, 160, 16000, -1), 2, 54, 100 * 400)
                //{
                //    GetDepthFraction = (_, _, _) => (1, 1)
                //},
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
                        sw.Write(",," + string.Join(", ", t.Select(t => t)));
                    }
                    sw.WriteLine();
                }

                foreach (var t in score.ind.Tuples)
                {
                    ulong[] tuples = t.Select(t => t).ToArray();

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
                Console.WriteLine(string.Join(", ", score.ind.Genome.Select(t => $"({string.Join(", ", t.Select(t => t.Size))})")));

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
                NumTuples = 9,
                SizeMin = 9,
                SizeMax = 9,
                MaxNumWeights = (int)Math.Pow(3, 11) * 1,
                MinDepth = 1,
                GenomeGenerator = rand => Enumerable.Range(0, 19).Select(_ => (float)rand.NextDouble()).ToArray(),
                Decoder = Decode,
            };

            int n_dimes = 4;
            int dime_size = 50;

            float n_factor = dime_size / 100F;
            int n_elites = (int)(20 * n_factor);
            int n_cx = (int)(60 * n_factor);
            int n_mutants = (int)(20 * n_factor);

            int n_start = 30;
            int n_end = 40;

            bool p(TrainingDataElement t) => n_start < t.board.n_stone && t.board.n_stone <= n_end;
            var data = Enumerable.Range(2001, 14).SelectMany(i => WthorRecordReader.Read($"WTH/WTH_{i}.wtb").SelectMany(x => x).Where(p)).OrderBy(i => Guid.NewGuid()).ToArray();
            var data_splited = ArrayUtil.Divide(data, n_dimes).Select(a => new TrainingData(a)).ToArray();

            var test_data = Enumerable.Range(2015, 1).SelectMany(i => WthorRecordReader.Read($"WTH/WTH_{i}.wtb").SelectMany(x => x).Where(p)).ToArray();

            var ga = new GA<float[], Score<float[]>>()
            {
                Info = info,
                Evaluator = new PopulationEvaluatorDistributed<float[], Score<float[]>>()
                {
                    Evaluators = n_dimes.Loop(i => new PopulationEvaluatorTrainingScorebySelfMatch<float[]>(new PopulationTrainer(1, 50, 32000, 2000))).ToArray(),
                },
                // Evaluator = new PopulationEvaluatorTrainingScoreKFoldWithVariableDepth<float[]>(data_splited),
                // Evaluator = new PopulationEvaluatorTrainingScoreShuffledKFold<float[]>(new TrainingData(data), 9600 * 24, 1200 * 24),
                Variator = new VariatorDistributed<float[]>()
                {
                    NumDime = n_dimes,
                    MigrationRate = 25,
                    Variator = new VariatorEliteArchive<float[]>()
                    {
                        NumElites = n_elites,
                        Groups = new VariationGroup<float[]>[] {
                            new VariationGroupOperation<float[]>(new CrossoverEliteBiased(0.7F), n_cx),
                            new VariationGroupRandom<float[]>(n_mutants),
                        },
                    },
                    MigrationTable = n_dimes.Loop(i => n_dimes.Loop(j => (i + 1) % n_dimes == j ? 10 : 0).ToArray()).ToArray(),
                },
            };

            string directory = $"ga/brkga_{DateTime.Now:yyyy_MM_dd_HH_mm}";
            Directory.CreateDirectory(directory);

            var log_tuple = $"{directory}/tuple.csv";
            using StreamWriter sw_tuple = File.AppendText(log_tuple);

            var log_score = $"{directory}/score.csv";
            using StreamWriter sw_score = File.AppendText(log_score);

            ga.Run(ga.Init(n_dimes * dime_size), (gen, time, pop) =>
            {
                foreach (var t in pop.Select((s, i) => (s, i)).OrderBy(t => t.i / dime_size).ThenBy(t => t.s.score))
                {
                    var s = t.s;
                    int id = t.i / 100;
                    int rank = t.i % 100;

                    sw_tuple.WriteLine($"{gen},{id},{s.score}," + string.Join(", ", s.ind.Tuples[0].Select(t => t)));
                }

                var top_inds = n_dimes.Loop(i => pop.Skip(dime_size * i).Take(dime_size).MinBy(s => s.score).First()).ToArray();

                var train_scores = top_inds.Select(s => s.score);

                var test_scores = top_inds.Select(s =>
                {
                    var w = s.ind.CreateWeight();
                    var trainer = new Trainer(w, 0.001F);
                    return trainer.TrainAndTest(data, test_data, s.ind.GetDepth());
                }).ToArray();

                // var top_scores = Enumerable.Range(0, pop.Count / 100).Select(i => pop[i * 100].score).ToArray();
                sw_score.WriteLine(string.Join(",", train_scores) + "," + string.Join(",", test_scores));

                var score = pop.MinBy(ind => ind.score).First();

                foreach (var t in score.ind.Tuples)
                {
                    foreach (var tt in t)
                    {
                        Console.WriteLine(TupleToString(tt));
                        Console.WriteLine();
                    }
                }

                Console.WriteLine($"Gen {gen}, {time:f1}s, score {score.score}");
                Console.WriteLine(string.Join(",", score.ind.Tuples.Select(t => $"({string.Join(", ", t.Select(Board.BitCount))})")));

                sw_tuple.Flush();
                sw_score.Flush();

                GC.Collect();
            });
        }

        public static string TupleToString(ulong mask)
        {
            Board b = new Board(mask, 0);
            string Disc(int i)
            {
                if ((mask & (1ul << i)) != 0)
                    return "X";
                else
                    return "-";
            }

            string Line(int y) => "| " + string.Join(" | ", 8.Loop(i => Disc(y * 8 + i))) + " |";

            return string.Join(Environment.NewLine, Line(0), Line(1), Line(2));
        }
    }

    public class GA<T, U> where U : Score<T>
    {
        public GenomeInfo<T> Info { get; set; }

        public IPopulationEvaluator<T, U> Evaluator { get; set; }
        public IVariator<T, U> Variator { get; set; }

        public void Run(List<Individual<T>> pop, Action<int, float, List<U>> logger)
        {
            var timer = new System.Diagnostics.Stopwatch();

            for (int i = 0; i < 10000; i++)
            {
                timer.Restart();

                Console.WriteLine("Evaluating");
                var scores = Evaluator.Evaluate(pop);

                timer.Stop();
                float time = (float)timer.ElapsedTicks / System.Diagnostics.Stopwatch.Frequency;

                Console.WriteLine("Logging");
                logger(i, time, scores);

                Console.WriteLine("Varying");
                pop = Variator.Vary(scores, i, Program.Random);
            }
        }

        public List<Individual<T>> Init(int n_pop)
        {
            Info.Init();
            return Enumerable.Range(0, n_pop).Select(_ => Info.Generate(Program.Random)).ToList();
        }
    }
}
