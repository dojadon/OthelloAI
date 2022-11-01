﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace OthelloAI
{
    static class Program
    {
        public const int NUM_STAGES = 60;

        public static readonly Pattern PATTERN_EDGE2X = Pattern.Create(new BoardHasherMask(0b01000010_11111111UL), NUM_STAGES, PatternType.X_SYMMETRIC, "e_edge_x.dat");
        public static readonly Pattern PATTERN_EDGE_BLOCK = Pattern.Create(new BoardHasherMask(0b00111100_10111101UL), NUM_STAGES, PatternType.X_SYMMETRIC, "e_edge_block.dat");
        public static readonly Pattern PATTERN_CORNER_BLOCK = Pattern.Create(new BoardHasherMask(0b00000111_00000111_00000111UL), NUM_STAGES, PatternType.XY_SYMMETRIC, "e_corner_block.dat");
        public static readonly Pattern PATTERN_CORNER = Pattern.Create(new BoardHasherMask(0b00000001_00000001_00000001_00000011_00011111UL), NUM_STAGES, PatternType.XY_SYMMETRIC, "e_corner.dat");
        public static readonly Pattern PATTERN_LINE1 = Pattern.Create(new BoardHasherLine1(1), NUM_STAGES, PatternType.X_SYMMETRIC, "e_line1.dat");
        public static readonly Pattern PATTERN_LINE2 = Pattern.Create(new BoardHasherLine1(2), NUM_STAGES, PatternType.X_SYMMETRIC, "e_line2.dat");
        public static readonly Pattern PATTERN_LINE3 = Pattern.Create(new BoardHasherLine1(3), NUM_STAGES, PatternType.X_SYMMETRIC, "e_line3.dat");
        public static readonly Pattern PATTERN_DIAGONAL8 = Pattern.Create(new BoardHasherMask(0x8040201008040201UL), NUM_STAGES, PatternType.DIAGONAL, "e_diag8.dat");
        public static readonly Pattern PATTERN_DIAGONAL7 = Pattern.Create(new BoardHasherMask(0x1020408102040UL), NUM_STAGES, PatternType.XY_SYMMETRIC, "e_diag7.dat");
        public static readonly Pattern PATTERN_DIAGONAL6 = Pattern.Create(new BoardHasherMask(0x10204081020UL), NUM_STAGES, PatternType.XY_SYMMETRIC, "e_diag6.dat");
        public static readonly Pattern PATTERN_DIAGONAL5 = Pattern.Create(new BoardHasherMask(0x102040810UL), NUM_STAGES, PatternType.XY_SYMMETRIC, "e_diag5.dat");

        public static readonly Pattern[] PATTERNS = { PATTERN_EDGE2X, PATTERN_EDGE_BLOCK, PATTERN_CORNER_BLOCK, PATTERN_CORNER, PATTERN_LINE1, PATTERN_LINE2 };

        static void Main()
        {
            // Test();
            Tester.TestC();
            // Tester.TestE();
            // GA.GATest.TestBRKGA();
            //Train();
            return;

            Console.WriteLine($"Support BMI2 : {System.Runtime.Intrinsics.X86.Bmi2.X64.IsSupported}");
            Console.WriteLine($"Support AVX2 : {System.Runtime.Intrinsics.X86.Avx2.IsSupported}");
            Console.WriteLine($"Support AVX : {System.Runtime.Intrinsics.X86.Avx.IsSupported}");

            Console.WriteLine();

            foreach (Pattern p in PATTERNS)
            {
                p.Load();
                Console.WriteLine(p);
            }

            // PATTERN_EDGE2X.Info(32, 2F);

            // Tester.Test1();
            // GA.GA<float[], GA.Score<float[]>>.TestBRKGA();
            // Test();
            // Train();
            // MPCParamSolver.Test();
            // StartUpdataEvaluation();
            // StartClient();
            // TestFFO();
            // StartGame();
            // StartManualGame();
            // UpdataEvaluationWithWthorDatabase();
            // UpdataEvaluationWithMyDatabase();
        }

        static void Test()
        {
            int count = 0;
            Pattern CreatePattern(ulong mask)
            {
                return Pattern.Create(new BoardHasherScanning(new BoardHasherMask(mask).Positions), 60, PatternType.ASYMMETRIC, $"{mask}_{count++}", true);
            }

            var s1 = "148451\r\n18403\r\n25571\r\n50887\r\n213955\r\n18375\r\n58083\r\n91075\r\n148423\r\n83907";
            var s2 = "10485\t312003\t395015\r\n10485\t312003\t460547\r\n10485\t279491\t460547\r\n10485\t279491\t395015\r\n12537\t279491\t460547\r\n10485\t312257\t460547\r\n12533\t312003\t395015\r\n12533\t312003\t460547\r\n12537\t312195\t460547\r\n10485\t312195\t460547";

            Pattern[][] p1 = s1.Split("\r\n").Select(ulong.Parse).Select(CreatePattern).Select(p => new[] {p}).ToArray();
            Pattern[][] p2 = s2.Split("\r\n").Select(t => t.Split("\t").Select(ulong.Parse).Select(CreatePattern).ToArray()).ToArray();

            PatternTrainer[] trainers = p1.Concat(p2).Select(p => new PatternTrainer(p, 0.005F)).ToArray();

            var evaluator = new EvaluatorRandomChoice(p1.Concat(p2).Select(p => new EvaluatorPatternBased(p)).ToArray());

            PlayerAI player = new PlayerAI(evaluator)
            {
                Params = new[] { new SearchParameters(depth: 5, stage: 0, SearchType.Normal, new CutoffParameters(true, true, false)),
                                              new SearchParameters(depth: 64, stage: 50, SearchType.Normal, new CutoffParameters(true, true, false))},
                PrintInfo = false,
            };

            for (int i = 0; i < 10000; i++)
            {
                var data = TrainerUtil.PlayForTrainingParallel(16, player);

                foreach (var trainer in trainers)
                {
                    foreach (var d in data)
                        trainer.Update(d.board, d.result);

                    Console.Write(trainer.Log.TakeLast(100000).Average() + ", ");
                }

                Console.WriteLine();

                if (i % 50 == 0)
                {
                    foreach (var p in p1.Concat(p2).SelectMany(p => p))
                        p.Save();
                }
            }
        }

        static void Train()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var trainer = new PatternTrainer(PATTERNS, 0.01F);

            for (int i = 0; i < 200; i++)
            {
                sw.Restart();

                var data = PlayForTraining(1000);
                float e = data.SelectMany(t => t.Item1.Select(b => trainer.Update(b, t.Item2))).Select(f => f * f).Average();

                sw.Stop();
                float time = (float)sw.ElapsedTicks / System.Diagnostics.Stopwatch.Frequency;

                string s_time = string.Format("{0:F}", time);
                Console.WriteLine($"{i}, {s_time}, {e}");

                foreach (var p in PATTERNS)
                {
                    p.ApplyTrainedEvaluation();
                    p.Save();
                }
            }
        }

        static IEnumerable<(List<Board>, int)> PlayForTraining(int n_game)
        {
            static bool Step(ref Board board, List<Board> boards, Player player, int stone)
            {
                (int x, int y, ulong move) = player.DecideMove(board, stone);
                if (move != 0)
                {
                    board = board.Reversed(move, stone);
                    boards.Add(board);
                    return true;
                }
                return false;
            }

            Evaluator evaluator = new EvaluatorPatternBased_Release();
            PlayerAI p = new PlayerAI(evaluator)
            {
                Params = new[] { new SearchParameters(depth: 6, stage: 0, SearchType.Normal, new CutoffParameters(true, true, false)),
                                              new SearchParameters(depth: 64, stage: 48, SearchType.Normal, new CutoffParameters(true, true, false))},
                PrintInfo = false,
            };

            int count = 0;

            var data = Enumerable.Range(0, 16).AsParallel().SelectMany(i =>
            {
                var results = new List<(List<Board>, int)>();
                var rand = new Random();

                while (count < n_game)
                {
                    Board board = Tester.CreateRnadomGame(rand, 8);
                    List<Board> boards = new List<Board>();

                    while (Step(ref board, boards, p, 1) | Step(ref board, boards, p, -1))
                    {
                    }
                    results.Add((boards, board.GetStoneCountGap()));

                    Interlocked.Increment(ref count);
                }

                return results;
            });

            return data.AsSequential();
        }

        static void UpdataEvaluationWithMyDatabase()
        {
            for (int i = 0; i <= 8; i++)
            {
                Console.WriteLine($"log{i}.dat");
                PatternTrainer.Train(PATTERNS, 0.005F, new MyRecordReader(@$"F:\Users\zyand\eclipse-workspace\tus\Report7\log\log{i}.dat"));
                Array.ForEach(PATTERNS, p => p.Save());
            }
        }

        static void UpdataEvaluationWithWthorDatabase()
        {
            for (int k = 0; k < 2; k++)
            {
                for (int i = 2001; i <= 2015; i++)
                {
                    Console.WriteLine($"{k} : WTH/WTH_{i}.wtb");
                    PatternTrainer.Train(PATTERNS, 0.005F, new WthorRecordReader($"WTH/WTH_{i}.wtb"));
                    Array.ForEach(PATTERNS, p => p.Save());
                }
            }
        }

        static void StartClient()
        {
            Evaluator evaluator = new EvaluatorPatternBased_Release();
            PlayerAI p = new PlayerAI(evaluator)
            {
                Params = new[] { new SearchParameters(depth: 9, stage: 0, SearchType.Normal, new CutoffParameters(true, true, false)),
                                              new SearchParameters(depth: 64, stage: 44, SearchType.Normal, new CutoffParameters(true, true, false))},
            };

            Client client = new Client(p);
            client.Run("localhost", 25033, "Gen2");
        }

        public static bool Step(ref Board board, PlayerAI player, int stone, bool print)
        {
            (_, _, ulong move, float e) = player.DecideMoveWithEvaluation(board, stone);
            if (move != 0)
            {
                board = board.Reversed(move, stone);
                if (print)
                {
                    Console.WriteLine($"Evaluation : {e}");
                    Console.WriteLine(board);
                }
                return true;
            }
            return false;
        }

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

        static void TestFFO()
        {
            Evaluator evaluator = new EvaluatorPatternBased_Release();
            PlayerAI p = new PlayerAI(evaluator)
            {
                Params = new[] { new SearchParameters(depth: 9, stage: 0, SearchType.Normal, new CutoffParameters(true, true, false)),
                                              new SearchParameters(depth: 64, stage: 40, SearchType.Normal, new CutoffParameters(true, true, false))},
            };

            string export = "No, Empty, Time, Nodes\r\n";

            for (int no = 40; no <= 44; no++)
            {
                string[] lines = File.ReadAllLines($"ffotest/end{no}.pos");
                (Board board, int color) = Parse(lines);
                Console.WriteLine(lines[1]);
                Console.WriteLine(color);
                Console.WriteLine(board);

                var sw = System.Diagnostics.Stopwatch.StartNew();

                p.SolveEndGame(board, p.Params[^1].cutoff_param);
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

        static void StartGame()
        {
            Board board;

            Evaluator evaluator = new EvaluatorPatternBased_Release();
            PlayerAI p = new PlayerAI(evaluator)
            {
                Params = new[] { new SearchParameters(depth: 9, stage: 0, SearchType.Normal, new CutoffParameters(true, true, false)),
                                              new SearchParameters(depth: 64, stage: 44, SearchType.Normal, new CutoffParameters(true, true, false))},
            };

            for (int i = 0; i < 1; i++)
            {
                board = new Board(Board.InitB, Board.InitW);
                // board = board.Reversed(Board.Mask(2, 3)).Reversed(Board.Mask(4, 2));

                while (Step(ref board, p, 1, true) | Step(ref board, p, -1, true))
                {
                    GC.Collect();
                }

                Console.WriteLine($"B: {board.GetStoneCount(1)}");
                Console.WriteLine($"W: {board.GetStoneCount(-1)}");
                Console.WriteLine(i);
            }

            //Console.WriteLine($"Average : {p.Times.Average()}");
            //Console.WriteLine($"Min : {p.Times.Min()}");
            //Console.WriteLine($"Max : {p.Times.Max()}");
            //Console.WriteLine();
            //Console.WriteLine(string.Join("\r\n", p.Times));
            //Console.WriteLine();
        }
    }
}
