using NumSharp;
using OthelloAI.Condingame;
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
            bool Step(PlayerAI player, int stone)
            {
                (_, _, ulong move, float e) = player.DecideMoveWithEvaluation(board, stone);

                if ((move & board.GetMoves(stone)) != 0)
                {
                    board = board.Reversed(move, stone);
                    return true;
                }
                return false;
            }

            while (Step(p1, 1) | Step(p2, -1))
            {
            }

            return board;
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
                    if ((move & board.GetMoves(stone)) == 0)
                    {
                        Console.WriteLine("Errrr");
                    }

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

        public static void TestBook()
        {
            var weight = new WeightEdax("eval.dat");
            var book = new Book("book.dat.store")
            //var book = new Book("20230226_level31_depth30_book_d5dx.dat")
            {
                Randomness = 1,
            };

            int w1 = 0;
            int w2 = 0;

            //Parallel.For(0, 1000, i => 
            for (int i = 0; i < 100; i++)
            {
                PlayerAI p1 = new PlayerAI(new EvaluatorRandomize(new EvaluatorWeightsBased(weight), 4))
                {
                    Params = new[] {
                    new SearchParameterFactory(stage: 0, type: SearchType.IterativeDeepening, depth: 8),
                    new SearchParameterFactory(stage: 44, type: SearchType.Normal, depth: 64) },
                    PrintInfo = false,
                    Book = book,
                };

                PlayerAI p2 = new PlayerAI(new EvaluatorRandomize(new EvaluatorWeightsBased(weight), 4))
                {
                    Params = new[] {
                    new SearchParameterFactory(stage: 0, type: SearchType.IterativeDeepening, depth: 8),
                    new SearchParameterFactory(stage: 44, type: SearchType.Normal, depth: 64) },
                    PrintInfo = false,
                };

                int result;

                if (i % 2 == 0)
                    result = Tester.PlayGame(p1, p2, Board.Init).GetStoneCountGap();
                else
                    result = -Tester.PlayGame(p2, p1, Board.Init).GetStoneCountGap();

                if (result > 0)
                    Interlocked.Increment(ref w1);
                else if (result < 0)
                    Interlocked.Increment(ref w2);

                float r = w1 / (float)(w1 + w2);
                Console.WriteLine($"{r}: {w1}/{w1 + w2}");
            }
            //);

            Console.WriteLine(book.Positions.Count);
            for (int i = 0; i < 10; i++)
            {
                Console.WriteLine(book.Counts.Count(t => t.Value > i));
            }
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
            var data = GamRecordReader.Read("WTH/xxx.gam").SelectMany(x => x.Where(Within)).ToArray();

            Console.WriteLine("Loading Tuples");

            var lines = File.ReadAllLines("ga/log_ga_2022_12_07_01_55_01_01_02.csv");
            var weights = CreateNetworkFromLogFile(lines, 0, 400, 20);

            var trainers = weights.Select(w => new Trainer(w, 0.001F)).ToArray();

            Console.WriteLine("Training");

            float e = trainers.AsParallel().Select(t => t.TrainAndTest(data[..90000], data[9000..])).Average();

            Console.WriteLine($"Data Var: {data.Select(d => d.result).AverageAndVariance()}");
            Console.WriteLine($"Avg Err: {e}");

            return;

            foreach (var d in data)
            {
                Console.WriteLine(d.board);
                Console.WriteLine($"{d.result}, {weights.Select(w => w.EvalTraining(new RotatedAndMirroredBoards(d.board))).Average()}");
            }
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

        public static ulong[][] GetMaskFromLine(string line)
        {
            var tokens = line.Split(",").Skip(3).ToArray();

            var list = new List<ulong[]>();
            var current = new List<ulong>();

            foreach (string s in tokens)
            {
                if (s.Length == 0)
                {
                    if (current.Count > 0)
                    {
                        list.Add(current.ToArray());
                        current = new List<ulong>();
                    }
                    continue;
                }
                current.Add(ulong.Parse(s));
            }

            list.Add(current.ToArray());

            return list.ToArray();
        }

        public static Weight CreateNetworkFromLine(string line)
        {
            var masks = GetMaskFromLine(line);

            foreach (var m in masks[0].OrderBy(Board.BitCount))
                Console.WriteLine(GATest.TupleToString(m) + Environment.NewLine);
            Console.WriteLine();

            if (masks.Length == 1)
                return new WeightsSum(masks[0].Select(u => new WeightArrayPextHashingTer(u)).ToArray());
            else
                return new WeightsStagebased(masks.Select(a => new WeightsSum(a.Select(u => new WeightArrayPextHashingTer(u)).ToArray())).ToArray());
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

        public static void TestDataset()
        {
            var data = GamRecordReader.Read("WTH/xxx.gam").Select(x => x.ToArray()).ToArray();
            Console.WriteLine(data.Length);

            float TestDistinct(IEnumerable<TrainingDataElement> data)
            {
                return data.Select(d => HashCode.Combine(d.board.GetHashCode(), d.result)).Distinct().Count();
            }

            Console.WriteLine(new Board(0b10000001_00000000_00000000_00000000_00000000_00000000_00000000_10000001, 0));

            bool ExistsAtCorner(ulong b)
            {
                return (b & 0b11000011_10000001_00000000_00000000_00000000_00000000_10000001_11000011) != 0;
            }

            float TestCorner(IEnumerable<TrainingDataElement> data)
            {
                return data.Count(d => ExistsAtCorner(d.board.bitB) || ExistsAtCorner(d.board.bitW));
            }

            for (int i = 0; i < 60; i++)
            {
                float f = TestCorner(data.Where(d => d.Length > i).Select(a => a[i]));
                Console.WriteLine($"{i}, {f}");
            }
        }

        public static void ConvertByteWeight()
        {
            string log_dir = "codingame";
            string path = log_dir + "/weight_6x6.dat";

            using var reader = new BinaryReader(new FileStream(path, FileMode.Open));

            int n = (int)Math.Pow(3, 6) * 6 * 6;

            byte[] data = new byte[n];

            static byte ConvertToInt8(float x, float range)
            {
                return (byte)Math.Clamp(x / range * 46 + 79, 32, 126);
            }

            for (int i = 0; i < n; i++)
            {
                float e = reader.ReadSingle();
                data[i] = ConvertToInt8(e, 4);
            }

            // File.WriteAllText(log_dir + "/e.csv", string.Join(Environment.NewLine, ee));

            string s = System.Text.Encoding.ASCII.GetString(data);
            byte[] data2 = System.Text.Encoding.ASCII.GetBytes(s);

            for (int i = 0; i < n; i++)
            {
                if (data[i] != data2[i])
                    Console.WriteLine($"{i}, {data[i]}, {data2[i]}");
            }

            Console.WriteLine(data.Length);
            Console.WriteLine(s.Length);
            Console.WriteLine(s);

            File.WriteAllText(log_dir + "/weight_6x6.txt", s);
        }

        public static void StartGame()
        {
            WeightLight.Init();
            BookLight.InitBook();

            var timer = new Stopwatch();

            (float[], int) Play(int i)
            {
                float[] t = new float[60];

                var board = Condingame.Board.Init;

                bool Step(int stone)
                {
                    // PlayerLight.use_probcut = stone == (i % 2 == 0 ? 1 : -1);
                    // BookLight.use_book = stone == (i % 2 == 0 ? 1 : -1);
                    BookLight.use_book = false;

                    timer.Restart();
                    (_, _, ulong move) = PlayerLight.DecideMove(board, stone);
                    timer.Stop();

                    if ((move & board.GetMoves(stone)) != 0)
                    {
                        t[board.n_stone - 4] = (float)timer.ElapsedTicks / Stopwatch.Frequency * 1000;

                        board = board.Reversed(move, stone);
                        // Console.WriteLine(board);
                        return true;
                    }
                    return false;
                }

                while (Step(1) | Step(-1)) { }

                return (t, board.GetStoneCountGap());
            }

            var times = new float[2][];
            int w1 = 1;
            int w2 = 1;

            for (int i = 0; i < times.Length; i++)
            {
                (float[] t, int result) = Play(i);

                if (i % 2 != 0)
                    result = -result;

                if (result > 0)
                    w1++;
                else if (result < 0)
                    w2++;

                times[i] = new float[60];

                for (int j = 0; j < 60; j++)
                    times[i][j] = t[j];

                Console.WriteLine($"{i}, {w1 / (float)(w1 + w2)}");
            }

            for (int j = 0; j < 60; j++)
            {
                float avg = times.Average(t => t[j]);
                float max = times.Max(t => t[j]);

                Console.WriteLine($"{j}, {avg}, {max}");
            }
        }

        public static void TrainWithDataset()
        {
            string log_dir = "codingame";

            var masks = Data.MASKS;

            Weight CreateFromMask(ulong[] m, int n_ply)
            {
                return new WeightsSum(m.Select(x => new WeightArrayPextHashingTer(x)).ToArray());
            }

            var weight = new WeightsStagebased6x6(masks.Select(CreateFromMask).ToArray());
            weight.Load(log_dir + "/weight_6x6.dat");

            var trainer = new Trainer(weight, 0.001F);

            // var data = GamRecordReader.Read("WTH/xxx.gam").SelectMany(x => x.ToArray()).ToArray();
            var data = Enumerable.Range(2001, 10).SelectMany(i => WthorRecordReader.Read($"WTH/WTH_{i}.wtb").SelectMany(x => x.ToArray())).ToArray();

            for (int i = 0; i < 10; i++)
            {
                trainer.Train(data);
                Console.WriteLine(i);
                weight.Save(log_dir + "/weight_6x6.dat");
            }
        }

        public static void Train()
        {
            string log_dir = "codingame";

            var masks = Data.MASKS;

            Weight CreateFromMask(ulong[] m, int n_ply)
            {
                return new WeightsSum(m.Select(x => new WeightArrayPextHashingTer(x)).ToArray());
            }

            var weight = new WeightsStagebased6x6(masks.Select(CreateFromMask).ToArray());
            weight.Load(log_dir + "/weight_6x6.dat");

            //var w = (WeightArrayPextHashingTer)((WeightsSum)weight.Weights[4]).Weights[4];
            //w.Test(3);

            // return;

            var trainer = new Trainer(weight, 0.00025F);

            var w_edax = new WeightEdax("eval.dat");

            PlayerAI CreatePlayer(Weight w, float v, int depth, int endgame)
            {
                var evaluator = new EvaluatorRandomize(new EvaluatorWeightsBased(w), v);
                return new PlayerAI(evaluator)
                {
                    PrintInfo = false,
                    Params = new[] {
                        new SearchParameterFactory(stage: 0, SearchType.Normal, depth),
                        new SearchParameterFactory(stage: endgame, SearchType.Normal, 64),
                    },
                };
            }

            TrainingData[] CreateData(int n, int d, int ed) => n.Loop().AsParallel().AsOrdered().Select(j =>
            {
                var rand = new Random();
                var p1 = CreatePlayer(weight, 16, d, ed);
                var p2 = CreatePlayer(w_edax, 2, d - 2, ed);
                if (j % 2 == 0)
                    return TrainerUtil.PlayForTraining(1, p1, p2, Board.Init);
                else
                    return TrainerUtil.PlayForTraining(1, p2, p1, Board.Init);
            }).ToArray();

            int depth = 8;
            int endgame = 44;

            var timer = new Stopwatch();

            return;

            using StreamWriter sw = File.CreateText(log_dir + $"/train.csv");

            for (int i = 0; i < 1000000; i++)
            {
                timer.Restart();

                var data = CreateData(30, depth, endgame);
                var e = trainer.Train(data.SelectMany(x => x));

                int[] fa = { 1, -1 };
                float r = 0.5F + 0.5F * data.Select((a, j) => Math.Clamp(a[^1].result * fa[j % 2], -1, 1)).Average();

                timer.Stop();
                var time = (float)timer.ElapsedTicks / Stopwatch.Frequency;

                Console.WriteLine($"{i}, {r:f2}, {e:f2}, {time:f2}s");
                sw.WriteLine($"{i}, {r:f2}, {e:f2}, {time:f2}");
                sw.Flush();

                if (i % 10 == 0)
                {
                    weight.Save(log_dir + "/weight_6x6.dat");
                }
            }
        }

        public static void TestParamMPC()
        {
            string log_dir = "codingame";

            var masks = Data.MASKS;

            static Weight CreateFromMask(ulong[] m, int n_ply)
            {
                return new WeightsSum(m.Select(x => new WeightArrayPextHashingTer(x)).ToArray());
            }

            var weight = new WeightsStagebased6x6(masks.Select(CreateFromMask).ToArray());
            weight.Load(log_dir + "/weight_6x6.dat");

            int depth = 7;
            int endgame = 46;

            var evaluator = new EvaluatorRandomize(new EvaluatorWeightsBased(weight), v: 16);
            var p = new PlayerAI(evaluator)
            {
                PrintInfo = false,
                Params = new[] {
                        new SearchParameterFactory(stage: 0, SearchType.Normal, depth),
                        new SearchParameterFactory(stage: endgame, SearchType.Normal, 64),
                    },
            };

            for (int i = 0; i < 50; i++)
            {
                Board board = Tester.PlayGame(p, p, Board.Init);
                Console.WriteLine(i);
            }

            for(int i = 0; i < 60; i++)
            {
                if (p.Errors[i].Count == 0)
                    continue;

                var t = p.Errors[i].AverageAndVariance();
                Console.WriteLine($"{i}, {t.avg}, {Math.Sqrt(t.var)}");
            }
        }

        public static void TestWeight2()
        {
            //string log_dir = "ga/brkga_2023_01_31_04_25";
            string log_dir = "ga/brkga_2023_03_07_20_46";
            int num_dimes = 8;
            int size_dime = 100;

            Weight CreateWeight(int gen, int d)
            {
                var lines = File.ReadAllLines(log_dir + "/tuple.csv");
                return CreateNetworkFromLine(lines[(gen * num_dimes + d) * size_dime]);
            }

            Weight[] CreateWeights(int gen)
            {
                var lines = File.ReadAllLines(log_dir + "/tuple.csv");
                return num_dimes.Loop(i => CreateNetworkFromLine(lines[(gen * num_dimes + i) * size_dime])).ToArray();
            }

            var weights =
                CreateWeights(900)
            // CreateWeights("ga/brkga_2023_01_25_14_16", 4000, 5, 100)
            .ConcatOne(new WeightsSum(MASKS.Select(m => new WeightArrayPextHashingBin(m)).ToArray())).ToArray();

            var trainers = weights.Select(w => new Trainer(w, 0.001F)).ToArray();

            var rand = new Random(100);
            var data = GamRecordReader.Read("WTH/xxx.gam").Select(x => x.ToArray()).OrderBy(_ => rand.Next()).ToArray();
            // var data = Enumerable.Range(2001, 10).SelectMany(i => WthorRecordReader.Read($"WTH/WTH_{i}.wtb").Select(x => x.ToArray())).ToArray();

            int n = 1;
            int n_train = (int)(data.Length * 0.8F);

            var result = new List<float[]>();

            for (int i = 0; i < 60 - n; i++)
            {
                var train_data = data[..n_train].Where(a => a.Length > i + n).SelectMany(a => a[i..(i + n)]).ToArray();
                var valid_data = data[n_train..].Where(a => a.Length > i + n).SelectMany(a => a[i..(i + n)]).ToArray();

                foreach (var w in weights)
                    w.Reset();

                float ScoreClass(float result, float eval)
                {
                    if (eval == 0)
                        return 0.5F;
                    else if (result > 0)
                        return eval > 0 ? 1 : 0;
                    else
                        return eval < 0 ? 1 : 0;
                }

                Parallel.ForEach(trainers, t => t.Train(train_data));

                //var e = trainers.AsParallel().Select(t => valid_data.Where(d => d.result != 0).Select(d => ScoreClass(d.result, t.Weight.EvalTraining(new RotatedAndMirroredBoards(d.board)))).Average()).ToArray();
                var e = trainers.AsParallel().Select(t => t.TestError(valid_data)).ToArray();
                result.Add(e);
                Console.WriteLine($"{i}, {string.Join(", ", e)}");
            }
            File.WriteAllLines(log_dir + "/test.csv", result.Select((e, i) => $"{i}, {string.Join(", ", e)}").ToArray());
        }

        public static void TestWeightAgainstEdaxNetwork()
        {
            string log_dir = "ga/brkga_2023_01_31_04_25";
            //string log_dir = "ga/brkga_2023_02_14_18_30";

            int num_dimes = 8;
            int size_dime = 100;

            int g = 4800;

            var lines = File.ReadAllLines(log_dir + "/tuple.csv");

            // var masks = GetMaskFromLine(lines[(g * num_dimes + d) * size_dime])[0];
            var masks = 8.Loop(i => GetMaskFromLine(lines[(g * num_dimes + i) * size_dime])[0]).ToArray();
            ulong[] SelectMasks(int i)
            {
                int idx = Math.Clamp((i - 28) / 4 + 3, 0, masks.Length - 1);
                Console.WriteLine($"{i}, {idx}");
                return masks[idx];
            }

            var w_edax = new WeightEdax("eval.dat");
            var tuner = w_edax.CreateFineTuner();

            Weight CreateFromMask(ulong[] m, int n_ply)
            {
                var w = m.Select(x => new WeightArrayPextHashingTer(x)).ToArray();
                //tuner.Apply(w, n_ply);

                return new WeightsSum(w);
            }

            var weights = new Weight[] {
                new WeightsStagebased60(61.Loop(i => i < 55 ? SelectMasks(i) : MASKS).Select(CreateFromMask).ToArray()),
                new WeightsStagebased60(61.Loop(i => CreateFromMask(MASKS, i)).ToArray()),
                // w_edax
            };

            // var weights = new[] { CreateNetworkFromLine(lines[(g * num_dimes + d) * size_dime]), new WeightsSum(MASKS.Select(m => new WeightArrayPextHashingBin(m)).ToArray()) };
            // var weights = new[] { CreateNetworkFromLine(lines[(g * num_dimes + d) * size_dime]), new WeightEdax("eval.dat") };
            // var weights = new[] { CreateNetworkFromLine(lines[0]), new WeightsSum(MASKS.Select(m => new WeightsArrayR(m)).ToArray()) };
            var trainers = weights.Select(w => new Trainer(w, 0.00025F)).ToArray();

            PlayerAI CreatePlayer(Weight w, int depth, int endgame)
            {
                var evaluator = new EvaluatorRandomize(new EvaluatorWeightsBased(w), v: 1);
                return new PlayerAI(evaluator)
                {
                    PrintInfo = false,
                    Params = new[] {
                        new SearchParameterFactory(stage: 0, SearchType.IterativeDeepening, depth),
                        new SearchParameterFactory(stage: endgame, SearchType.Normal, 64),
                    },
                };
            }

            var opening_data = GamRecordReader.Read("WTH/xxx.gam").Select(x => x.ToArray()).Where(a => a.Length > 30).ToArray();

            using StreamWriter sw = File.CreateText(log_dir + $"/test_wr_{g}.csv");

            TrainingData[] CreateData(int n, int d, int ed) => n.Loop().AsParallel().AsOrdered().Select(j =>
            {
                var rand = new Random();
                var board = rand.Choice(opening_data)[12].board;

                var p1 = CreatePlayer(weights[0], d, ed);
                var p2 = CreatePlayer(weights[1], d, ed);

                if (j % 2 == 0)
                    return TrainerUtil.PlayForTraining(1, p1, p2, board);
                else
                    return TrainerUtil.PlayForTraining(1, p2, p1, board);
            }).ToArray();

            int depth = 6;
            int endgame = 46;

            for (int i = 0; i < 1000000; i++)
            {
                var data = CreateData(64, depth, endgame);

                var e = trainers.Select(t => t.Train(data.SelectMany(x => x))).ToArray();
                // var e = trainers[0].Train(data.SelectMany(x => x));

                int[] fa = { 1, -1 };
                float r = 0.5F + 0.5F * data.Select((a, j) => Math.Clamp(a[^1].result * fa[j % 2], -1, 1)).Average();

                Console.WriteLine($"{i}, {r}, {e[0]}, {e[1]}");
                sw.WriteLine($"{i}, {r}, {e[0]}, {e[1]}");

                // Console.WriteLine($"{i}, {r}, {e}");
                // sw.WriteLine($"{i}, {r}, {e}");
                sw.Flush();

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
