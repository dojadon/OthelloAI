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
                NumTuples = 6,
                // NetworkSizes = GenomeInfo<float[]>.CreateSimpleNetworkSizes(9, 9, (int)Math.Pow(3, 11) * 1),
                // NetworkSizes = new[] { new[] { 10, 10, 10, 9, 8, 8, 8, 8, 7, 6, 5, 4 } },
                NetworkSizes = new[] { 6.Loop(_ => 6).ToArray() } ,
                MinDepth = 1,
                GenomeGenerator = rand => Enumerable.Range(0, 24).Select(_ => (float)rand.NextDouble()).ToArray(),
                Decoder = Decode,
            };

            int[] t = 8.Loop(i => 20 + i * 5).ToArray();
            int range = 2;

            Func<TrainingDataElement, bool> Within(int i)
            {
                return t => i - range <= t.board.n_stone && t.board.n_stone < i + range;
            };

            int n_dimes = t.Length;
            int dime_size = 100;

            float n_factor = dime_size / 100F;
            int n_elites = (int)(20 * n_factor);

            var variation_groups = new VariationGroup<float[]>[]
            {
                new VariationGroupOperation2<float[]>(new CrossoverEliteBiased(0.7F), (int)(60 * n_factor)),
                // new VariationGroupOperation1<float[]>(new MutantSwap<float[]>(), (int)(10 * n_factor)),
                new VariationGroupRandom<float[]>( (int)(20 * n_factor)),
            };

            var rand = new Random(100);
            var data = GamRecordReader.Read("WTH/xxx.gam").Select(x => x.ToArray()).OrderBy(_ => rand.Next()).ToArray();

            var data_i = t.Select(i => data.Select(x => x.Where(Within(i)).ToArray()).ToArray()).ToArray();

            int n_train = (int)(121123 * 0.8F);

            var train_data_i = data_i.Select(d => d[..n_train]).ToArray();
            var test_data_i = data_i.Select(d => d[n_train..]).ToArray();

            var ga = new GA<float[], Score<float[]>>()
            {
                Info = info,
                Evaluator = new PopulationEvaluatorDistributed<float[], Score<float[]>>()
                {
                    // Evaluators = n_dimes.Loop(i => new PopulationEvaluatorTrainingScorebySelfMatch<float[]>(new PopulationTrainer(1, 50), 4800, 800) { Tuner = tuner }).ToArray(),
                    Evaluators = train_data_i.Select(d => new PopulationEvaluatorTrainingScoreShuffledKFold<float[]>(d, 0.8F, 0.2F)).ToArray(),
                },
                // Evaluator = new PopulationEvaluatorTrainingScoreShuffledKFold<float[]>(data, 0.8F, 0.2F),
                Variator = new VariatorDistributed<float[]>()
                {
                    NumDime = n_dimes,
                    Variator = new VariatorEliteArchive<float[]>()
                    {
                        NumElites = n_elites,
                        Groups = variation_groups,
                    },
                    MigrationRate = 25,
                    MigrationSize = 10,
                    MigrationModel = new MigrationModelLine()
                    {
                        Range = 1
                    },
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
                    int id = t.i / dime_size;
                    int rank = t.i % dime_size;

                    sw_tuple.WriteLine($"{gen},{id},{s.score},," + string.Join(",,", s.ind.Tuples.Select(a => string.Join(", ", a.Select(t => t)))));
                }

                var top_inds = n_dimes.Loop(i => pop.Skip(dime_size * i).Take(dime_size).MinBy(s => s.score).First()).ToArray();

                var train_scores = top_inds.Select(s => s.score);
                var test_scores = n_dimes.Loop().Select(i =>
                {
                    var s = top_inds[i];
                    var trainer = new Trainer(s.ind.CreateWeight(), 0.001F);
                    return trainer.TrainAndTest(train_data_i[i].SelectMany(x => x), test_data_i[i].SelectMany(x => x));
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
            return Enumerable.Range(0, n_pop).Select(_ => Info.Generate(Program.Random)).ToList();
        }
    }
}
