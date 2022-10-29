﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OthelloAI
{
    class Tester
    {
        public static Board CreateRnadomGame(Random rand, int num_moves)
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

        public static Board PlayGame(Board init, Player p1, Player p2)
        {
            static bool Step(ref Board board, Player player, int stone)
            {
                (_, _, ulong move) = player.DecideMove(board, stone);
                if (move != 0)
                {
                    board = board.Reversed(move, stone);
                    return true;
                }
                return false;
            }

            Board board = init;
            while (Step(ref board, p1, 1) | Step(ref board, p2, -1))
            {
            }
            return board;
        }

        public static void TestB()
        {
            Pattern[] patterns = Program.PATTERNS;

            foreach (var p in patterns)
                p.Load();

            int Play(PlayerAI p1, PlayerAI p2)
            {
                bool Step(ref Board board, Player player, int stone)
                {
                    (int x, int y, ulong move) = player.DecideMove(board, stone);

                    if (move != 0)
                    {
                        board = board.Reversed(move, stone);
                        return true;
                    }
                    return false;
                }

                Board board = Tester.CreateRnadomGame(GA.GATest.Random, 8);
                while (Step(ref board, p1, 1) | Step(ref board, p2, -1))
                {
                }

                return board.GetStoneCountGap();
            }

            int n = 8;

            for (int i = 1; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    PlayerAI p1 = new PlayerAI(new EvaluatorPatternBased(patterns))
                    {
                        ParamBeg = new SearchParameters(depth: i, stage: 0, new CutoffParameters(true, true, false)),
                        ParamMid = new SearchParameters(depth: i, stage: 16, new CutoffParameters(true, true, false)),
                        ParamEnd = new SearchParameters(depth: 64, stage: 48, new CutoffParameters(true, true, false)),
                        PrintInfo = false,
                    };

                    // p1.Depth_Prob = i * 0.2F;

                    PlayerAI p2 = new PlayerAI(new EvaluatorPatternBased(patterns))
                    {
                        ParamBeg = new SearchParameters(depth: j, stage: 0, new CutoffParameters(true, true, false)),
                        ParamMid = new SearchParameters(depth: j, stage: 16, new CutoffParameters(true, true, false)),
                        ParamEnd = new SearchParameters(depth: 64, stage: 48, new CutoffParameters(true, true, false)),
                        PrintInfo = false,
                    };

                    // p2.Depth_Prob = j * 0.2F;

                    double avg = Enumerable.Range(0, 500).AsParallel().Select(i =>
                    {
                        return Play(p1, p2) switch
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

        public static void TestA()
        {
            Pattern[] patterns = Program.PATTERNS;

            foreach (var p in patterns)
                p.Load();

            int Play(PlayerAI p1, PlayerAI p2, List<long> count)
            {
                bool Step(ref Board board, Player player, int stone)
                {
                    if (board.n_stone >= 50)
                        return false;

                    (int x, int y, ulong move) = player.DecideMove(board, stone);
                    count.Add(p1.SearchedNodeCount);

                    if (move != 0)
                    {
                        board = board.Reversed(move, stone);
                        return true;
                    }
                    return false;
                }

                Board board = Tester.CreateRnadomGame(GA.GATest.Random, 8);
                while (Step(ref board, p1, 1) | Step(ref board, p2, -1))
                {
                }

                return board.GetStoneCountGap();
            }

            for (int i = 0; i < 11; i++)
            {
                PlayerAI player = new PlayerAI(new EvaluatorPatternBased(patterns))
                {
                    ParamBeg = new SearchParameters(depth: 3, stage: 0, new CutoffParameters(true, true, false)),
                    ParamMid = new SearchParameters(depth: 3, stage: 16, new CutoffParameters(true, true, false)),
                    ParamEnd = new SearchParameters(depth: 64, stage: 48, new CutoffParameters(true, true, false)),
                    PrintInfo = false,
                };

                player.Depth_Prob = i * 0.1F;

                double avg = Enumerable.Range(0, 1000).AsParallel().SelectMany(i =>
                {
                    List<long> c = new();
                    Play(player, player, c);
                    return c;
                }).Average();

                Console.WriteLine($"{i}, {avg}");
            }
        }

        public static void TestC()
        {
            var rand = new Random();
            Pattern[] patterns = Program.PATTERNS;

            foreach (var p in patterns)
                p.Load();

            PlayerAI[] players = Enumerable.Range(0, 4).Select(i => new PlayerAI(new EvaluatorPatternBased(patterns))
            {
                ParamBeg = new SearchParameters(depth: 1 + i * 2, stage: 0, new CutoffParameters(true, true, false)),
                ParamMid = new SearchParameters(depth: 1 + i * 2, stage: 16, new CutoffParameters(true, true, false)),
                ParamEnd = new SearchParameters(depth: 64, stage: 54 - i, new CutoffParameters(true, true, false)),
                PrintInfo = false,

            }).ToArray();

            IEnumerable<float>[][] Play()
            {
                List<float>[][] evaluations = Enumerable.Range(0, 64).Select(_ => Enumerable.Range(0, players.Length).Select(_ => new List<float>()).ToArray()).ToArray();

                bool Step(ref Board board, int stone)
                {
                    ulong move = 0;

                    if (20 < board.n_stone && board.n_stone < 50)
                    {
                        for (int i = 0; i < players.Length; i++)
                        {
                            (_, _, move, float e) = players[i].DecideMoveWithEvaluation(board, stone);
                            evaluations[board.n_stone][i].Add(e * stone);
                        }
                    }
                    else
                    {
                        (_, _, move) = players[^1].DecideMove(board, stone);
                    }

                    if (move != 0)
                    {
                        board = board.Reversed(move, stone);
                        return true;
                    }
                    return false;
                }

                Board board = Board.Init;

                while (board.n_stone < 60)
                {
                    board = Tester.CreateRnadomGame(GA.GATest.Random, 8);
                    while (Step(ref board, 1) | Step(ref board, -1))
                    {
                    }
                }

                int result = board.GetStoneCountGap();
                float Error(float x) => (x - result) * (x - result);

                return evaluations.Select(l1 => l1.Select(l2 => l2.Select(Error)).ToArray()).ToArray();
            }

            var e = Enumerable.Range(0, 1000).AsParallel().Select(i => Play()).ToArray();

            var log = $"test/log_{DateTime.Now:yyyy_MM_dd_HH_mm}.csv";
            using StreamWriter sw = File.AppendText(log);

            for (int i = 0; i < 64; i++)
            {
                if (20 < i && i < 50)
                {
                    var error = Enumerable.Range(0, players.Length).Select(j => e.SelectMany(l => l[i][j]).Where(f => f < 1000).Average()).ToArray();
                    sw.WriteLine(string.Join(",", error));
                }
            }
        }

        public static void TestE()
        {
            var rand = new Random();
            var timer = new System.Diagnostics.Stopwatch();
            int n = (int)1E+7;

            BoardHasher CreateRandomHasher(int n)
            {
                // return new BoardHasherMask(rand.GenerateRegion(24, n));
                return new BoardHasherScanning(new BoardHasherMask(rand.GenerateRegion(24, n)).Positions);
            }

            for (int size = 2; size < 11; size++)
            {
                var patterns = Enumerable.Range(0, 100).Select(_ => Pattern.Create(CreateRandomHasher(size), 1, PatternType.ASYMMETRIC)).ToArray();
                timer.Reset();

                for (int i = 0; i < n; i++)
                {
                    var p = rand.Choice(patterns);
                    var b = new RotatedAndMirroredBoards(rand.NextBoard());

                    timer.Start();
                    p.Eval(b);
                    timer.Stop();
                }

                var time_s = (double)timer.ElapsedTicks / System.Diagnostics.Stopwatch.Frequency / n;
                var time_nano = time_s * 1E+9;
                Console.WriteLine($"{size}, {time_nano}");
            }
        }
    }
}
