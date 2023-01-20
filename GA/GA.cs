using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

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
                NumTuples = 12,
                //NetworkSizes = GenomeInfo<float[]>.CreateSimpleNetworkSizes(9, 9, (int)Math.Pow(3, 11) * 1),
                NetworkSizes = new[] { new[] { 10, 10, 10, 9, 8, 8, 8, 8, 7, 6, 5, 4 } } ,
                //NetworkSizes = new[] { 11.Loop(_ => 9).ToArray() } ,
                MinDepth = 1,
                GenomeGenerator = rand => Enumerable.Range(0, 19).Select(_ => (float)rand.NextDouble()).ToArray(),
                Decoder = Decode,
            };

            int n_dimes = 4;
            int dime_size = 100;

            float n_factor = dime_size / 100F;
            int n_elites = (int)(20 * n_factor);
            int n_cx = (int)(60 * n_factor);
            int n_mutants = (int)(20 * n_factor);

            int n_start = 29;
            int n_end = 30;

            bool p(TrainingDataElement t) => n_start <= t.board.n_stone && t.board.n_stone <= n_end;

            var data = GamRecordReader.Read("WTH/xxx.gam").SelectMany(x => x.Where(p)).ToArray();

            int n_train = (int)(121123 * 0.8F) * (n_end - n_start + 1);

            var train_data = data[..n_train];
            var test_data = data[n_train..];

            var weight_edax = new WeightEdax("eval.dat");
            var tuner = weight_edax.CreateFineTuner();

            var ga = new GA<float[], Score<float[]>>()
            {
                Info = info,
                //Evaluator = new PopulationEvaluatorDistributed<float[], Score<float[]>>()
                //{
                //    Evaluators = n_dimes.Loop(i => new PopulationEvaluatorTrainingScorebySelfMatch<float[]>(new PopulationTrainer(1, 50), 4800, 800) { Tuner = tuner }).ToArray(),
                //    // Evaluators = n_dimes.Loop(i => new PopulationEvaluatorTournament<float[]>(new PopulationTrainer(1, 50), 32000, 3200, 3, 48)).ToArray(),
                //},
                // Evaluator = new PopulationEvaluatorTrainingScoreKFoldWithVariableDepth<float[]>(data_splited),
                Evaluator = new PopulationEvaluatorTrainingScoreShuffledKFold2<float[]>(data, 0.8F, 0.2F),
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
                    int id = t.i / dime_size;
                    int rank = t.i % dime_size;

                    sw_tuple.WriteLine($"{gen},{id},{s.score},," + string.Join(",,", s.ind.Tuples.Select(a => string.Join(", ", a.Select(t => t)))));
                }

                var top_inds = n_dimes.Loop(i => pop.Skip(dime_size * i).Take(dime_size).MinBy(s => s.score).First()).ToArray();

                var train_scores = top_inds.Select(s => s.score);
                var test_scores = top_inds.AsParallel().Select(s =>
                {
                    var w = s.ind.CreateWeight();
                    var trainer = new Trainer(w, 0.001F);
                    return trainer.TrainAndTest(train_data, test_data);
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
