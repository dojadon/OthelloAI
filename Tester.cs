using System;
using System.IO;
using System.Linq;

namespace OthelloAI
{
    public static class Tester
    {
        public static Board PlayGame(PlayerAI p1, PlayerAI p2, Board board)
        {
            return PlayGame(p1, p2, board, (_, _, _, _) => { });
        }

        public static Board PlayGame(PlayerAI p1, PlayerAI p2, Board board, Action<Board, int, ulong, float> action)
        {
            return PlayGame<object>(p1, p2, board, (b, c, m, e) => { action(b, c, m, e); return null; }).b;
        }

        public static (Board b, T[] array) PlayGame<T>(PlayerAI p1, PlayerAI p2, Board board, Func<Board, int, ulong, float, T> action)
        {
            return PlayGame(p1, p2, board, action, () => default);
        }

        public static (Board b, T[] array) PlayGame<T>(PlayerAI p1, PlayerAI p2, Board board, Func<Board, int, ulong, float, T> action, Func<T> generator)
        {
            var array = Enumerable.Range(0, 64).Select(_ => generator()).ToArray();

            bool Step(PlayerAI player, int stone)
            {
                (_, _, ulong move, float e) = player.DecideMoveWithEvaluation(board, stone);

                if (move != 0)
                {
                    array[board.n_stone] = action(board, stone, move, e);
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
            Weights weight = null;
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

        public static long[][] TestSearchedNodesCount(Weights weight, float depth, int n_games)
        {
            var player = new PlayerAI(new EvaluatorWeightsBased(weight))
            {
                Params = new[] { new SearchParameters(stage: 0, type: SearchType.Normal, depth: depth),
                                              // new SearchParameters(stage: 60, type: SearchType.Normal, depth: 64)
                },
                PrintInfo = false,
            };

            return Enumerable.Range(0, n_games).AsParallel().Select(i => PlayGame(player, player, CreateRandomGame(8), (b, c, m, e) => player.SearchedNodeCount).array).ToArray();
        }

        public static long[][][] TestSearchedNodesCount(Weights weight, float[] depths, int n_games)
        {
            var players = depths.Select(d => new PlayerAI(new EvaluatorWeightsBased(weight))
            {
                Params = new[] { new SearchParameters(stage: 0, type: SearchType.Normal, depth: d),
                                              // new SearchParameters(stage: 60, type: SearchType.Normal, depth: 64)
                },
                PrintInfo = false,
            }).ToArray();

            return Enumerable.Range(0, n_games).AsParallel().Select(i => PlayGame(players[^1], players[^1], CreateRandomGame(8), (b, c, m, e) =>
            {
                foreach (var p in players[0..^1])
                {
                    p.DecideMove(b, c);
                }
                return players.Select(p => p.SearchedNodeCount).ToArray();
            }, () => new long[players.Length]).array).ToArray();
        }

        public static float[][][] TestError(Weights weight, float[] depths, int n_games)
        {
            var players = depths.Select(d => new PlayerAI(new EvaluatorWeightsBased(weight))
            {
                Params = new[] { new SearchParameters(stage: 0, type: SearchType.Normal, depth: d),
                                              // new SearchParameters(stage: 60, type: SearchType.Normal, depth: 64)
                },
                PrintInfo = false,
            }).ToArray();

            return Enumerable.Range(0, n_games).AsParallel().Select(i =>
            {
                (var board, var t) = PlayGame(players[^1], players[^1], CreateRandomGame(8), (b, c, m, e) =>
                {
                    return players[0..^1].Select(p => p.DecideMoveWithEvaluation(b, c).e * c).Concat(new[] { e * c }).ToArray();
                }, () => new float[players.Length]);

                int result = board.GetStoneCountGap();
                float Error(float e) => (e - result) * (e - result);

                return t.Select(tt => tt.Select(Error).ToArray()).ToArray();

            }).ToArray();
        }

        public static (Board b, float[][] e) TestError(Weights weight, float[] depths)
        {
            var players = depths.Select(d => new PlayerAI(new EvaluatorWeightsBased(weight))
            {
                Params = new[] { new SearchParameters(stage: 0, type: SearchType.Normal, depth: d),
                                              // new SearchParameters(stage: 60, type: SearchType.Normal, depth: 64)
                },
                PrintInfo = false,
            }).ToArray();

            return PlayGame(players[^1], players[^1], CreateRandomGame(8), (b, c, m, e) =>
            {
                return players[0..^1].Select(p => p.DecideMoveWithEvaluation(b, c).e * c).Concat(new[] { e * c }).ToArray();
            }, () => new float[players.Length]);
        }

        public static double[] TestEvaluationTime(int n_times, int n_weights, int[] sizes, string type)
        {
            var rand = new Random();
            var timer = new System.Diagnostics.Stopwatch();

            BoardHasher CreateRandomHasher(int size) => type switch
            {
                "pext" => new BoardHasherMask(rand.GenerateRegion(24, size)),
                "scan" => new BoardHasherScanning(new BoardHasherMask(rand.GenerateRegion(24, size)).Positions),
                _ => null
            };

            return sizes.Select(size =>
            {
                var patterns = Enumerable.Range(0, n_weights).Select(_ => Weights.Create(CreateRandomHasher(size), 1)).ToArray();
                timer.Reset();

                for (int i = 0; i < n_times; i++)
                {
                    var p = rand.Choice(patterns);
                    var b = new RotatedAndMirroredBoards(rand.NextBoard());

                    timer.Start();
                    p.Eval(b);
                    timer.Stop();
                }

                var time_s = (double)timer.ElapsedTicks / System.Diagnostics.Stopwatch.Frequency / n_times;
                return time_s * 1E+9;
            }).ToArray();
        }
    }
}
