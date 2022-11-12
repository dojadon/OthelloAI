using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using NumSharp;

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
            Board Step(Board b)
            {
                Move[] moves = new Move(b).NextMoves();
                Move move = moves[rand.Next(moves.Length)];
                return move.reversed;
            }

            Board board = Board.Init;

            for (int i = 0; i < num_moves; i++)
            {
                board = Step(board);
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

                p.SolveEndGame(board, p.Params[^1].CreateCutoffParameters(board.n_stone));
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

        public static void TestB()
        {
            Weight weight = null;
            // weight.Load();

            int n = 8;

            for (int i = 1; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    PlayerAI p1 = new PlayerAI(new EvaluatorWeightsBased(weight))
                    {
                        Params = new[] { new SearchParameters(stage: 0, type: SearchType.Normal, depth: i),
                                              new SearchParameters(stage: 48, type: SearchType.Normal, depth: 64)},
                        PrintInfo = false,
                    };

                    // p1.Depth_Prob = i * 0.2F;

                    PlayerAI p2 = new PlayerAI(new EvaluatorWeightsBased(weight))
                    {
                        Params = new[] { new SearchParameters(stage: 0, type: SearchType.Normal, depth: j),
                                              new SearchParameters(stage: 48, type: SearchType.Normal, depth: 64)},
                        PrintInfo = false,
                    };

                    // p2.Depth_Prob = j * 0.2F;

                    double avg = Enumerable.Range(0, 500).AsParallel().Select(i =>
                    {
                        return PlayGame(p1, p2, CreateRandomGame(8)).GetStoneCountGap() switch
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
                Params = new[] { new SearchParameters(stage: 0, type: SearchType.Normal, depth: depth),
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
                Params = new[] { new SearchParameters(stage: 0, type: SearchType.Normal, depth: d),
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
                Params = new[] { new SearchParameters(stage: 0, type: SearchType.Normal, depth: depth),
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
                Params = new[] { new SearchParameters(stage: 0, type: SearchType.Normal, depth: d),
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

        public static float[][][] TestError(Weight weight, IEnumerable<float> depths, int n_games)
        {
            var players = depths.Select(d => new PlayerAI(new EvaluatorWeightsBased(weight))
            {
                Params = new[] { new SearchParameters(stage: 0, type: SearchType.Normal, depth: d),
                                              new SearchParameters(stage: 50, type: SearchType.Normal, depth: 64)
                },
                PrintInfo = false,
            }).ToArray();

            return Enumerable.Range(0, n_games).AsParallel().AsOrdered().Select(i =>
            {
                if (i % 100 == 0)
                    Console.WriteLine(i);

                (var board, var t) = TestError(players);

                int result = board.GetStoneCountGap();
                float Error(float e) => (e - result) * (e - result);

                return t.Select(tt => tt.Select(Error).ToArray()).ToArray();
            }).ToArray();
        }

        public static (Board b, float[][] e) TestError(PlayerAI[] players)
        {
            return PlayGame(players[^1], players[^1], CreateRandomGame(8), r =>
            {
                return players[0..^1].Select(p => p.DecideMoveWithEvaluation(r.prev_board, r.color).e * r.color).Concat(new[] { r.evaluation * r.color }).ToArray();
            }, () => new float[players.Length]);
        }

        public static double[] TestEvaluationTime(int n_times, IEnumerable<int> sizes, string type)
        {
            Weight Create(int size, Random rand) => type switch
            {
                "pext" => new WeightsArrayR(rand.GenerateRegion(24, size)),
                "scan" => new WeightsArrayS(rand.GenerateRegion(24, size)),
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

                        timer.Start();
                        weight.Eval(new RotatedAndMirroredBoards(b));
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

            var log = $"G:/マイドライブ/Lab/test/log_e_time_{DateTime.Now:yyyy_MM_dd_HH_mm}.csv";
            using StreamWriter sw = File.AppendText(log);

            double[] t1 = Tester.TestEvaluationTime(n, sizes, "pext");
            double[] t2 = Tester.TestEvaluationTime(n, sizes, "scan");

            for (int i = 0; i < sizes.Length; i++)
            {
                Console.WriteLine($"{sizes[i]}, {t1[i]}, {t2[i]}");
                sw.WriteLine($"{sizes[i]}, {t1[i]}, {t2[i]}");
            }
        }
    }
}
