using NumSharp;
using OthelloAI.GA;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OthelloAI
{
    public readonly struct SearchedResult
    {
        public readonly Board prev_board;
        public readonly Board next_board;
        public readonly int color;
        public readonly ulong move;
        public readonly float evaluation;
        public readonly double time_s;

        public SearchedResult(Board prev_board, int color, ulong move, float evaluation, double time_s) : this()
        {
            this.prev_board = prev_board;
            this.next_board = prev_board.Reversed(move, color);
            this.color = color;
            this.move = move;
            this.evaluation = evaluation;
            this.time_s = time_s;
        }
    }

    public static class Tester
    {
        public static Board PlayGame(PlayerAI p1, PlayerAI p2, Board board)
        {
            return PlayGame(p1, p2, board, _ => { });
        }

        public static Board PlayGame(PlayerAI p1, PlayerAI p2, Board board, Action<SearchedResult> action)
        {
            return PlayGame<object>(p1, p2, board, r => { action(r); return null; }).b;
        }

        public static (Board b, T[] array) PlayGame<T>(PlayerAI p1, PlayerAI p2, Board board, Func<SearchedResult, T> action)
        {
            return PlayGame(p1, p2, board, action, () => default);
        }

        public static (Board b, T[] array) PlayGame<T>(PlayerAI p1, PlayerAI p2, Board board, Func<SearchedResult, T> action, Func<T> generator)
        {
            var array = Enumerable.Range(0, 64).Select(_ => generator()).ToArray();
            var timer = new Stopwatch();

            bool Step(PlayerAI player, int stone)
            {
                timer.Restart();
                (_, _, ulong move, float e) = player.DecideMoveWithEvaluation(board, stone);
                timer.Stop();

                if (move != 0)
                {
                    double time_s = (double)timer.ElapsedTicks / Stopwatch.Frequency;
                    array[board.n_stone] = action(new SearchedResult(board, stone, move, e, time_s));
                    board = board.Reversed(move, stone);
                    return true;
                }
                return false;
            }

            while (Step(p1, 1) | Step(p2, -1))
            {
            }
            return (board, array);
        }

        public static Board CreateRandomGame(int num_moves)
        {
            return CreateRandomGame(num_moves, Program.Random);
        }

        public static Board CreateRandomGame(int num_moves, Random rand)
        {
            return CreateRandomGame(num_moves, rand, Board.Init);
        }

        public static Board CreateRandomGame(int num_moves, Random rand, Board init)
        {
            Board board = init;

            Player player = new PlayerRandom(rand);
            int n = 0;

            bool Step(int stone)
            {
                (_, _, ulong move) = player.DecideMove(board, stone);

                if (move != 0)
                {
                    board = board.Reversed(move, stone);
                    return ++n < num_moves;
                }
                return false;
            }

            while (Step(1) | Step(-1))
            {
            }

            return board;
        }

        static void TestFFO(PlayerAI p)
        {
            static (Board, int) Parse(string[] lines)
            {
                Board b = new Board(Enumerable.Range(0, 64).Select(i => lines[0][i] switch
                {
                    'X' => 1,
                    'O' => -1,
                    _ => 0
                }).ToArray());

                return (b, lines[1].StartsWith("Black") ? 1 : -1);
            }

            string export = "No, Empty, Time, Nodes\r\n";

            for (int no = 40; no <= 44; no++)
            {
                string[] lines = File.ReadAllLines($"ffotest/end{no}.pos");
                (Board board, int color) = Parse(lines);
                Console.WriteLine(lines[1]);
                Console.WriteLine(color);
                Console.WriteLine(board);

                var sw = System.Diagnostics.Stopwatch.StartNew();

                p.SolveRoot(new Search(), board, p.Params[^1].CreateSearchParameter(1));
                sw.Stop();
                float time = 1000F * sw.ElapsedTicks / System.Diagnostics.Stopwatch.Frequency;

                Console.WriteLine($"FFO Test No.{no}");
                Console.WriteLine($"Discs : {64 - board.n_stone}");
                Console.WriteLine($"Taken Time : {time} ms");
                Console.WriteLine($"Nodes : {p.SearchedNodeCount}");

                export += $"{no}, {64 - board.n_stone}, {time}, {p.SearchedNodeCount}\r\n";
            }

            File.WriteAllText($"FFO_Test_{DateTime.Now:yyyy_MM_dd_HH_mm}.csv", export);
        }

        public static void TestA()
        {
            var weight = Program.WEIGHT;
            weight.Load("e.dat");

            var e = new EvaluatorWeightsBased(weight);
            var e1 = new EvaluatorRandomize(e, 20 / 10 * 127);
            var e2 = new EvaluatorRandomize(e, 70 / 10 * 127);

            PlayerAI p1 = new PlayerAI(e1)
            {
                Params = new[] { new SearchParameterFactory(stage: 0, type: SearchType.Normal, depth: 4),
                                              new SearchParameterFactory(stage: 50, type: SearchType.Normal, depth: 64)},
                PrintInfo = false,
            };

            PlayerAI p2 = new PlayerAI(e2)
            {
                Params = new[] { new SearchParameterFactory(stage: 0, type: SearchType.Normal, depth: 6),
                                              new SearchParameterFactory(stage: 50, type: SearchType.Normal, depth: 64)},
                PrintInfo = false,
            };

            int count = 0;
            int w1 = 0;

            Parallel.For(0, 500, i =>
            {
                int result;
                if (i % 2 == 0)
                {
                    result = PlayGame(p1, p2, CreateRandomGame(5)).GetStoneCountGap();
                }
                else
                {
                    result = -PlayGame(p2, p1, CreateRandomGame(5)).GetStoneCountGap();
                }

                if (result == 0)
                    return;

                Interlocked.Add(ref count, 1);
                if (result > 0)
                    Interlocked.Add(ref w1, 1);

                Console.WriteLine($"{w1}, {count - w1}");
            });
        }

        public static void TestB()
        {
            var weight = Program.WEIGHT;
            weight.Load("e.dat");

            int n = 8;

            for (int i = 1; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    PlayerAI p1 = new PlayerAI(new EvaluatorWeightsBased(weight))
                    {
                        Params = new[] { new SearchParameterFactory(stage: 0, type: SearchType.Normal, depth: i),
                                              new SearchParameterFactory(stage: 48, type: SearchType.Normal, depth: 64)},
                        PrintInfo = false,
                    };

                    PlayerAI p2 = new PlayerAI(new EvaluatorWeightsBased(weight))
                    {
                        Params = new[] { new SearchParameterFactory(stage: 0, type: SearchType.Normal, depth: j),
                                              new SearchParameterFactory(stage: 48, type: SearchType.Normal, depth: 64)},
                        PrintInfo = false,
                    };

                    double avg = Enumerable.Range(0, 500).AsParallel().Select(i =>
                    {
                        return PlayGame(p1, p2, CreateRandomGame(5)).GetStoneCountGap() switch
                        {
                            < 0 => 0,
                            > 0 => 1,
                            0 => 0.5
                        };
                    }).Average();

                    Console.WriteLine($"{i}, {j}, {avg}");
                }
            }
        }

        public static double[][] TestSearchedTime(Weight weight, float depth, int n_games)
        {
            var player = new PlayerAI(new EvaluatorWeightsBased(weight))
            {
                Params = new[] { new SearchParameterFactory(stage: 0, type: SearchType.Normal, depth: depth),
                                              // new SearchParameters(stage: 60, type: SearchType.Normal, depth: 64)
                },
                PrintInfo = false,
            };

            return Enumerable.Range(0, n_games).AsParallel().Select(i => PlayGame(player, player, CreateRandomGame(8), r => r.time_s).array).ToArray();
        }

        public static double[][][] TestSearchedTime(Weight weight, float[] depths, int n_games)
        {
            var players = depths.Select(d => new PlayerAI(new EvaluatorWeightsBased(weight))
            {
                Params = new[] { new SearchParameterFactory(stage: 0, type: SearchType.Normal, depth: d),
                                              // new SearchParameters(stage: 60, type: SearchType.Normal, depth: 64)
                },
                PrintInfo = false,
            }).ToArray();

            return Enumerable.Range(0, n_games).AsParallel().Select(i => PlayGame(players[^1], players[^1], CreateRandomGame(8), r =>
            {
                foreach (var p in players[0..^1])
                {
                    p.DecideMove(r.prev_board, r.color);
                }
                return players.Select(p => p.TakenTime).ToArray();
            }, () => new double[players.Length]).array).ToArray();
        }

        public static long[][] TestSearchedNodesCount(Weight weight, float depth, int n_games)
        {
            var player = new PlayerAI(new EvaluatorWeightsBased(weight))
            {
                Params = new[] { new SearchParameterFactory(stage: 0, type: SearchType.Normal, depth: depth),
                                              // new SearchParameters(stage: 60, type: SearchType.Normal, depth: 64)
                },
                PrintInfo = false,
            };

            return Enumerable.Range(0, n_games).AsParallel().Select(i => PlayGame(player, player, CreateRandomGame(8), r => player.SearchedNodeCount).array).ToArray();
        }

        public static double[][][][] TestSearchingPerformance(Weight weight, IEnumerable<float> depths, int n_games)
        {
            var players = depths.Select(d => new PlayerAI(new EvaluatorWeightsBased(weight))
            {
                Params = new[] { new SearchParameterFactory(stage: 0, type: SearchType.Normal, depth: d),
                                              // new SearchParameters(stage: 60, type: SearchType.Normal, depth: 64)
                },
                PrintInfo = false,
            }).ToArray();

            return Enumerable.Range(0, n_games).AsParallel().Select(i => PlayGame(players[^1], players[^1], CreateRandomGame(8), r =>
            {
                foreach (var p in players[0..^1])
                {
                    p.DecideMove(r.prev_board, r.color);
                }
                return players.Select(p => new double[] { p.SearchedNodeCount, p.TakenTime }).ToArray();
            }, () => players.Select(_ => new double[2]).ToArray()).array).ToArray();
        }

        public static void TestPerformance()
        {
            var log = $"G:/マイドライブ/Lab/test/log_s_per_{DateTime.Now:yyyy_MM_dd_HH_mm}.csv";
            using StreamWriter sw = File.AppendText(log);

            var t = Tester.TestSearchingPerformance(Program.WEIGHT, Enumerable.Range(0, 6).Select(i => i * 1F), 5000);
            var a = np.array(t).mean(0);

            for (int i = 0; i < a.shape[0]; i++)
            {
                Console.WriteLine(string.Join(", ", a[$"{i}, :, 0"].ToArray<double>()) + ",," + string.Join(", ", a[$"{i}, :, 1"].ToArray<double>()));
                sw.WriteLine(string.Join(", ", a[$"{i}, :, 0"].ToArray<double>()) + ",," + string.Join(", ", a[$"{i}, :, 1"].ToArray<double>()));
            }
        }

        public static float[][][] TestError(Weight weight, IEnumerable<float> depths, int depth_index, int n_games)
        {
            var players = depths.Select(d => new PlayerAI(new EvaluatorWeightsBased(weight))
            {
                Params = new[] { new SearchParameterFactory(stage: 0, type: SearchType.Normal, depth: d),
                                              new SearchParameterFactory(stage: 50, type: SearchType.Normal, depth: 64)
                },
                PrintInfo = false,
            }).ToArray();

            return Enumerable.Range(0, n_games).AsParallel().AsOrdered().Select(i =>
            {
                if (i % 100 == 0)
                    Console.WriteLine(i);

                (var board, var t) = TestError(players, depth_index);

                int result = board.GetStoneCountGap();
                float Error(float e) => (e - result) * (e - result);

                return t.Select(tt => tt.Select(Error).ToArray()).ToArray();
            }).ToArray();
        }

        public static (Board b, float[][] e) TestError(PlayerAI[] players, int index)
        {
            return PlayGame(players[index], players[index], CreateRandomGame(8), r =>
            {
                return players.Select((p, i) => r.color * (i == index ? r.evaluation : p.DecideMoveWithEvaluation(r.prev_board, r.color).e)).ToArray();
            }, () => new float[players.Length]);
        }

        public static (float[][] eval, float[][] time, float[][] nodes) TestPerformance(PlayerAI[] players, IEnumerable<TrainingDataElement> data)
        {
            var result = data.Select(t =>
            {
                float Err(float e) => (e - t.result) * (e - t.result);

                return players.Select(p =>
                {
                    p.SearchedNodeCount = 0;
                    var timer = Stopwatch.StartNew();
                    float eval = p.Evaluate(t.board);
                    timer.Stop();

                    float time = (float)timer.ElapsedTicks / Stopwatch.Frequency;
                    return new float[] { Err(eval), time, p.SearchedNodeCount };
                }).ToArray();

            }).ToArray();

            var a = Enumerable.Range(0, 3).Select(i => Enumerable.Range(0, players.Length).Select(j => result.Select(a => a[j][i]).ToArray()).ToArray()).ToArray();

            return (a[0], a[1], a[2]);
        }

        public static void TestError2()
        {
            var weight = Program.WEIGHT;
            var depths = Enumerable.Range(0, 8).Select(i => i * 1);

            weight.Load(@"G:\マイドライブ\Lab\e\e.dat");

            var players = depths.Select(d => new PlayerAI(new EvaluatorWeightsBased(weight))
            {
                Params = new[] {
                    new SearchParameterFactory(stage: 0, type: SearchType.Normal, depth: d),
                },
                PrintInfo = false,
            }).ToArray();

            static bool Within(TrainingDataElement d) => 39 <= d.board.n_stone && d.board.n_stone <= 40;

            var data = WthorRecordReader.Read(@"G:\マイドライブ\Lab\WTH\WTH_2001.wtb").SelectMany(x => x).Where(Within);

            (float[][] eval, float[][] time, float[][] nodes) = TestPerformance(players, data);

            var f1 = Regression.Exponential(depths.Select(d => d * 1F), eval.Select(a => a.Average()));
            Console.WriteLine($"{f1}");

            Console.WriteLine(string.Join(", ", eval.Select(a => a.Average())));
            Console.WriteLine(string.Join(", ", time.Select(a => a.Average())));
            Console.WriteLine(string.Join(", ", nodes.Select(a => a.Average())));
        }

        public static void TestError3()
        {
            Console.WriteLine("Loading Train Data");

            static bool Within(TrainingDataElement d) => 40 <= d.board.n_stone && d.board.n_stone <= 40;
            var data = Enumerable.Range(2001, 15).Select(i => WthorRecordReader.Read($"WTH/WTH_{i}.wtb").SelectMany(x => x).Where(Within).ToArray()).ToArray();

            Console.WriteLine("Loading Tuples");

            var lines = File.ReadAllLines("ga/log_ga_2022_12_07_01_55_01_01_02.csv");
            var weights = CreateNetworkFromLogFile(lines, 0, 400, 20);

            var trainers = weights.Select(w => new Trainer(w, 0.001F)).ToArray();

            Console.WriteLine("Training");

            float e = trainers.AsParallel().Select(t => t.TrainAndTest(data[1..^2].SelectMany(x => x), data[^1])).Average();

            Console.WriteLine($"Avg Err: {e}");

            Console.WriteLine("Testing");

            var depths = Enumerable.Range(0, 6).Select(i => i * 0.25F);

            var results = weights.AsParallel().Select(w =>
            {
                var players = depths.Select(d => new PlayerAI(new EvaluatorWeightsBased(w))
                {
                    Params = new[] { new SearchParameterFactory(stage: 0, type: SearchType.IterativeDeepening, depth: d), },
                    PrintInfo = false,
                }).ToArray();

                (float[][] eval, float[][] time, float[][] nodes) = TestPerformance(players, data[^1]);

                string line = string.Join(", ", eval.Select(a => a.Average())) + ",," + string.Join(", ", time.Select(a => a.Average())) + ",," + string.Join(", ", nodes.Select(a => a.Average()));

                return line;
            }).ToArray();

            File.WriteAllLines($"test/test_performance_{DateTime.Now:yyyy_MM_dd_HH_mm}.csv", results);
        }

        public static double[] TestEvaluationTime(int n_times, IEnumerable<int> sizes, string type)
        {
            Weight Create(int size, Random rand) => type switch
            {
                "pext" => new WeightArrayPextHashingBin(rand.GenerateRegion(24, size)),
                "scan" => new WeightArrayScanning(rand.GenerateRegion(24, size)),
                _ => null
            };

            int n = 10;

            return (new int[] { 2 }).Concat(sizes).Select(size =>
            {
                var timer = new Stopwatch();
                var rand = new Random();

                for (int i = 0; i < n; i++)
                {
                    var weight = Create(size, rand);

                    for (int j = 0; j < n_times / n; j++)
                    {
                        var b = rand.NextBoard();
                        var bb = new RotatedAndMirroredBoards(b);

                        timer.Start();
                        weight.Eval(bb);
                        timer.Stop();
                    }
                }

                var time_s = (double)timer.ElapsedTicks / Stopwatch.Frequency / n_times;
                return time_s * 1E+9;
            }).Skip(1).ToArray();
        }

        public static void TestEvaluationTime()
        {
            int n = 10000000;
            var sizes = Enumerable.Range(2, 10).ToArray();

            var log = $"test/log_e_time_{DateTime.Now:yyyy_MM_dd_HH_mm}.csv";
            using StreamWriter sw = File.AppendText(log);

            double[] t1 = Tester.TestEvaluationTime(n, sizes, "pext");
            double[] t2 = Tester.TestEvaluationTime(n, sizes, "scan");

            for (int i = 0; i < sizes.Length; i++)
            {
                Console.WriteLine($"{sizes[i]}, {t1[i]}, {t2[i]}");
                sw.WriteLine($"{sizes[i]}, {t1[i]}, {t2[i]}");
            }
        }

        public static Weight CreateNetworkFromLine(string line)
        {
            var tokens = line.Split(",").Where(s => s.Length > 0).Skip(3);
            var masks = tokens.Select(ulong.Parse).ToArray();

            //foreach (var m in masks)
            //    Console.WriteLine(new Board(m, 0));

            double avg = masks.Select(m1 => MASKS.Select(m2 =>Math.Pow(3, Board.BitCount(m1 & m2))).Sum()).Average();
            double min = masks.Select(m1 => MASKS.Select(m2 => Math.Pow(3, Board.BitCount(m1 & m2))).Sum()).Min();
            double max = masks.Select(m1 => MASKS.Select(m2 => Math.Pow(3, Board.BitCount(m1 & m2))).Sum()).Max();
            Console.WriteLine($"{avg}, {min}, {max}");

            return new WeightsSum(masks.Select(u => new WeightArrayPextHashingBin(u)).ToArray());
        }

        public static Weight[] CreateNetworkFromLogFile(string[] lines, int gen, int pop_size, int n_top)
        {
            int idx = gen * pop_size;
            return lines[idx..(idx + n_top)].Select(CreateNetworkFromLine).ToArray();
        }

        public static void TestEvalVar()
        {
            var rand = new Random();
            Weight[] weights = 20.Loop(i => new WeightsSum(4.Loop(_ => new WeightArrayPextHashingBin(rand.GenerateRegion(24, 8))).ToArray())).ToArray();

            var trainer = new PopulationTrainer(2, 50);

            var rank_his = new List<int[]>();

            using StreamWriter sw = File.CreateText($"test/test_eval_var_(num20_8x4)_trainer(2,50,2400,200)_{DateTime.Now:yyyy_MM_dd_HH_mm}.csv");

            for (int i = 0; i < 50; i++)
            {
                var scores = trainer.TrainAndTest(weights, 2400, 400);
                var rank = scores.Select(s1 => scores.Count(s2 => s2 < s1) < 5 ? 1 : 0).ToArray();
                rank_his.Add(rank);

                var avg = weights.Length.Loop(i => rank_his.Select(a => a[i]).Average()).ToArray();
                var var = weights.Length.Loop(i => rank_his.Select(a => a[i]).Variance()).ToArray();

                sw.WriteLine(string.Join(",", rank) + ",," + string.Join(",", var));

                Console.WriteLine(string.Join(", ", avg));
                // Console.WriteLine(var.Average());
            }
        }

        public static ulong[] MASKS =  {
            0b01000010_11111111U,
            0b10111101_10111101U,
            0b00000111_00000111_00000111UL,
            0b00000001_00000001_00000001_00000011_00011111UL,
            0b11111111_00000000UL,
            0b11111111_00000000_00000000UL,
            0b11111111_00000000_00000000_00000000UL,
            0x8040201008040201UL,
            0x1020408102040UL,
            0x10204081020UL,
            0x102040810UL,
        };

        public static void TestGAResult()
        {
            static float CalcExeCost(Weight weight, int n_dsics)
            {
                float t_factor = 2.5F;
                float cost_per_node = 480F;

                return cost_per_node + weight.NumOfEvaluation(n_dsics) * t_factor;
            }

            static float GetDepth(Weight weight, int n_dsics)
            {
                float min_depth = 1;
                float max_t = 20 * 9 + 480F;

                float t = CalcExeCost(weight, n_dsics);

                return (float)Math.Log(max_t / t) / 1.1F + min_depth;
            }

            int num_dimes = 8;
            int size_dime = 100;

            static bool p1(TrainingDataElement t) => 34 <= t.board.n_stone && t.board.n_stone <= 40;
            static bool p2(TrainingDataElement t) => 32 <= t.board.n_stone && t.board.n_stone <= 34;

            var data = Enumerable.Range(2001, 10).SelectMany(i => WthorRecordReader.Read($"WTH/WTH_{i}.wtb").Select(x => x.Where(p1).ToArray())).OrderBy(i => Guid.NewGuid()).ToArray();

            // var test_data = WthorRecordReader.Read($"WTH/WTH_2015.wtb").SelectMany(x => x).Where(p2).ToArray();
            var test_data = Enumerable.Range(2014, 2).SelectMany(i => WthorRecordReader.Read($"WTH/WTH_{i}.wtb").SelectMany(x => x).Where(p1)).OrderBy(i => Guid.NewGuid()).ToArray(); ;

            WeightsSum[] weights = 3.Loop(i => new WeightsSum(MASKS.Select(m => new WeightArrayPextHashingBin(m)).ToArray())).ToArray();
            // Weight[] weights = 6.Loop(i => new WeightsSum(masks.Take(i + 1).Select(m => new WeightsArrayR(m)).ToArray())).ToArray();
            var trainers = weights.Select(w => new Trainer(w, 0.001F)).ToArray();

            for (int i = 0; i < 500; i++)
            {
                TrainingDataElement[] d = data[i];
                Parallel.ForEach(trainers, t => t.Train(d));

                if (i % 10 == 0)
                {
                    var e = trainers.AsParallel().Select(t => t.TestError(test_data, 1)).ToArray();
                    Console.WriteLine(string.Join(",", e));
                }
            }

            //string log_dir = $"ga/brkga_2022_12_28_18_14";
            //var lines = File.ReadAllLines(log_dir + "/tuple.csv");
            //using StreamWriter sw = File.CreateText(log_dir + "/test.csv");

            //for (int gen = 0; gen <= 2500; gen++)
            //{
            //    var weights = num_dimes.Loop(i => lines[(gen * num_dimes + i) * size_dime]).Select(CreateNetworkFromLine);
            //    // var scores = weights.AsParallel().Select(w => Trainer.KFoldTest(w, GetDepth(w, 40), data_splited)).ToArray();
            //    var scores = weights.AsParallel().Select(w => new Trainer(w, 0.001F).TrainAndTest(data, test_data)).ToArray();

            //    Console.WriteLine($"{gen}: " + string.Join(",", scores));
            //    sw.WriteLine(string.Join(",", scores));
            //}
        }

        public static void TestWeightAgainstEdaxNetwork()
        {
            string log_dir = "ga/brkga_2023_01_10_08_24";

            int num_dimes = 4;
            int size_dime = 100;

            int g = 1106;
            int d = 1;

            var lines = File.ReadAllLines(log_dir + "/tuple.csv");

            // var weights = new[] { CreateNetworkFromLine(lines[(g * num_dimes + d) * size_dime]), new WeightsSum(MASKS.Select(m => new WeightArrayPextHashingBin(m)).ToArray()) };
            var weights = new[] { CreateNetworkFromLine(lines[(g * num_dimes + d) * size_dime]), new WeightEdax() };
            weights[1].Load("eval.dat");
            // var weights = new[] { CreateNetworkFromLine(lines[0]), new WeightsSum(MASKS.Select(m => new WeightsArrayR(m)).ToArray()) };
            var trainers = weights.Select(w => new Trainer(w, 0.0001F)).ToArray();

            PlayerAI CreatePlayer(Weight w, int depth, int endgame)
            {
                var evaluator = new EvaluatorRandomize(new EvaluatorWeightsBased(w), v: 8);
                return new PlayerAI(evaluator)
                {
                    PrintInfo = false,
                    Params = new[] {
                        new SearchParameterFactory(stage: 0, SearchType.Normal, depth),
                        new SearchParameterFactory(stage: endgame, SearchType.Normal, 64),
                    },
                };
            }

            using StreamWriter sw = File.CreateText(log_dir + $"/test_wr_{g}_{d}.csv");

            bool Within(TrainingDataElement d) => 10 <= d.board.n_stone && d.board.n_stone <= 54;

            TrainingData[] CreateData(int n, int d) => n.Loop().AsParallel().Select(j =>
            {
                var p1 = CreatePlayer(weights[0], d, 50);
                var p2 = CreatePlayer(weights[1], d, 50);

                if (j % 2 == 0)
                    return TrainerUtil.PlayForTraining(1, p1, p2);
                else
                    return TrainerUtil.PlayForTraining(1, p2, p1);
            }).ToArray();

            for (int i = 0; i < 20000; i++)
            {
                var data = CreateData(32, 2);

                // var e = trainers.Select(t => t.Train(data.SelectMany(x => x).Where(Within))).ToArray();
                var e = trainers[0].Train(data.SelectMany(x => x).Where(Within));

                //Console.WriteLine($"{i} / {NumTrainingGames / 16}");
                // float r = data.Select((d, j) => d[^1].result * (j % 2 == 0 ? 1 : -1)).Select(r => Math.Clamp(r, -0.5F, 0.5F) + 0.5F).Average();

                if (i % 10 == 0)
                {
                    float r = MeasureWinRate(weights[0], weights[1]);

                    var test_data = CreateData(32, 5);
                    // var test_loss = trainers.Select(t => t.TestError(test_data.SelectMany(x => x), 1)).ToArray();
                    var test_loss = new[] { 0, 0 };

                    Console.WriteLine($"{i}, {r}, {test_loss[0]}, {test_loss[1]}");
                    sw.WriteLine($"{i}, {r}, {test_loss[0]}, {test_loss[1]}");
                    sw.Flush();
                }
            }

            float MeasureWinRate(Weight w1, Weight w2)
            {
                PlayerAI p1 = CreatePlayer(w1, 5, 48);
                PlayerAI p2 = CreatePlayer(w2, 5, 48);

                int c1 = 0;
                int c2 = 0;

                Parallel.For(0, 100, i =>
                {
                    int result;

                    if (i % 2 == 0)
                        result = PlayGame(p1, p2, Board.Init).GetStoneCountGap();
                    else
                        result = -PlayGame(p2, p1, Board.Init).GetStoneCountGap();

                    if (result > 0)
                        Interlocked.Increment(ref c1);
                    else if (result < 0)
                        Interlocked.Increment(ref c2);
                });

                return (float)c1 / (c1 + c2);
            }
        }

        public static void TestWeights()
        {
            string log_dir = "ga/brkga_2023_01_11_14_26";

            int num_dimes = 4;
            int size_dime = 50;

            var lines = File.ReadAllLines(log_dir + "/tuple.csv");

            var gens = 100.Loop(i => i * 1).ToArray();
            var weights = gens.SelectMany(g => num_dimes.Loop(d => lines[(g * num_dimes + d) * size_dime])).Select(CreateNetworkFromLine).ToArray();
            //weights = weights.ConcatOne(new WeightsSum(MASKS.Select(m => new WeightsArrayR(m)).ToArray())).ToArray();
            //var weights = new[] { CreateNetworkFromLine(lines[(209 * num_dimes + 2) * size_dime]), new WeightsSum(MASKS.Select(m => new WeightsArrayR(m)).ToArray()) };

            if (true)
            {
                var trainer = new PopulationTrainer(2, 48);
                var scores = trainer.TrainAndTest(weights, 6400, 1200);

                Console.WriteLine(string.Join(",", scores));
                // File.WriteAllText(log_dir + "/test.csv", scores[^1] + Environment.NewLine + string.Join(Environment.NewLine, gens.Length.Loop(i => string.Join(",", num_dimes.Loop(d => scores[i * num_dimes + d])))));

                return;
            }

            //Directory.CreateDirectory(log_dir + "/e");
            //for (int i = 0; i < weights.Length; i++)
            //{
            //    weights[i].Save(log_dir + $"/e/{i}.dat");
            //}

            static PlayerAI CreatePlayer(Weight w)
            {
                return new PlayerAI(new EvaluatorRandomize(new EvaluatorWeightsBased(w), 5F))
                {
                    Params = new[] {
                        new SearchParameterFactory(stage: 0, type: SearchType.IterativeDeepening, depth: 7),
                        new SearchParameterFactory(stage: 48, type: SearchType.Normal, depth: 64),
                    },
                    PrintInfo = false,
                };
            }

            float MeasureWinRate(Weight w1, Weight w2)
            {
                PlayerAI p1 = CreatePlayer(w1);
                PlayerAI p2 = CreatePlayer(w2);

                int c1 = 0;
                int c2 = 0;

                Parallel.For(0, 1000, i =>
                {
                    int result;

                    if (i % 2 == 0)
                        result = PlayGame(p1, p2, Board.Init).GetStoneCountGap();
                    else
                        result = -PlayGame(p2, p1, Board.Init).GetStoneCountGap();

                    if (result > 0)
                        Interlocked.Increment(ref c1);
                    else if (result < 0)
                        Interlocked.Increment(ref c2);

                    Console.WriteLine($"{c1}:{c2}, {c1 / (float)(c1 + c2)}");
                });

                return (float)c1 / (c1 + c2);
            }

            var history = new List<float>();

            for (int i = 0; i < weights.Length - 1; i++)
            {
                float rate = MeasureWinRate(weights[i], weights[^1]);
                Console.WriteLine($"{i}, {rate}");
                history.Add(rate);
            }

            // File.WriteAllText(log_dir + "/test_win.csv", string.Join(Environment.NewLine, history));
        }
    }
}
