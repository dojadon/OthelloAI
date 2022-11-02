using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace OthelloAI
{
    static class Program
    {
        public const int NUM_STAGES = 60;

        public static readonly Weights PATTERN_EDGE2X = Weights.Create(new BoardHasherMask(0b01000010_11111111UL), NUM_STAGES, "e_edge_x.dat", true);
        public static readonly Weights PATTERN_EDGE_BLOCK = Weights.Create(new BoardHasherMask(0b00111100_10111101UL), NUM_STAGES, "e_edge_block.dat", true);
        public static readonly Weights PATTERN_CORNER_BLOCK = Weights.Create(new BoardHasherMask(0b00000111_00000111_00000111UL), NUM_STAGES, "e_corner_block.dat", true);
        public static readonly Weights PATTERN_CORNER = Weights.Create(new BoardHasherMask(0b00000001_00000001_00000001_00000011_00011111UL), NUM_STAGES, "e_corner.dat", true);
        public static readonly Weights PATTERN_LINE1 = Weights.Create(new BoardHasherLine1(1), NUM_STAGES, "e_line1.dat", true);
        public static readonly Weights PATTERN_LINE2 = Weights.Create(new BoardHasherLine1(2), NUM_STAGES, "e_line2.dat", true);

        public static readonly Weights WEIGHT = new WeightsSum(new[] { PATTERN_EDGE2X, PATTERN_EDGE_BLOCK, PATTERN_CORNER_BLOCK, PATTERN_CORNER, PATTERN_LINE1, PATTERN_LINE2 });

        static void Main()
        {
            // Test();
            // Tester.TestF();
            // Tester.TestE();
            GA.GATest.TestBRKGA();
            //Train();
            return;

            Console.WriteLine($"Support BMI2 : {System.Runtime.Intrinsics.X86.Bmi2.X64.IsSupported}");
            Console.WriteLine($"Support AVX2 : {System.Runtime.Intrinsics.X86.Avx2.IsSupported}");
            Console.WriteLine($"Support AVX : {System.Runtime.Intrinsics.X86.Avx.IsSupported}");
            Console.WriteLine();

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
            //int count = 0;
            //Pattern CreatePattern(ulong mask)
            //{
            //    return Pattern.Create(new BoardHasherScanning(new BoardHasherMask(mask).Positions), 60, PatternType.ASYMMETRIC, $"{mask}_{count++}", true);
            //}

            //var s1 = "148451\r\n18403\r\n25571\r\n50887\r\n213955\r\n18375\r\n58083\r\n91075\r\n148423\r\n83907";
            //var s2 = "10485\t312003\t395015\r\n10485\t312003\t460547\r\n10485\t279491\t460547\r\n10485\t279491\t395015\r\n12537\t279491\t460547\r\n10485\t312257\t460547\r\n12533\t312003\t395015\r\n12533\t312003\t460547\r\n12537\t312195\t460547\r\n10485\t312195\t460547";

            //Pattern[][] p1 = s1.Split("\r\n").Select(ulong.Parse).Select(CreatePattern).Select(p => new[] {p}).ToArray();
            //Pattern[][] p2 = s2.Split("\r\n").Select(t => t.Split("\t").Select(ulong.Parse).Select(CreatePattern).ToArray()).ToArray();

            //PatternTrainer[] trainers = p1.Concat(p2).Select(p => new PatternTrainer(p, 0.005F)).ToArray();

            //var evaluator = new EvaluatorRandomChoice(p1.Concat(p2).Select(p => new EvaluatorWeightsBased(p)).ToArray());

            //PlayerAI player = new PlayerAI(evaluator)
            //{
            //    Params = new[] { new SearchParameters(depth: 5, stage: 0, type: SearchType.Normal),
            //                                  new SearchParameters(depth: 64, stage: 50, type: SearchType.Normal)},
            //    PrintInfo = false,
            //};

            //for (int i = 0; i < 10000; i++)
            //{
            //    var data = TrainerUtil.PlayForTrainingParallel(16, player);

            //    foreach (var trainer in trainers)
            //    {
            //        foreach (var d in data)
            //            trainer.Update(d.board, d.result);

            //        Console.Write(trainer.Log.TakeLast(100000).Average() + ", ");
            //    }

            //    Console.WriteLine();

            //    if (i % 50 == 0)
            //    {
            //        foreach (var p in p1.Concat(p2).SelectMany(p => p))
            //            p.Save();
            //    }
            //}
        }

        static void StartClient()
        {
            Evaluator evaluator = new EvaluatorPatternBased_Release();
            PlayerAI p = new PlayerAI(evaluator)
            {
                Params = new[] { new SearchParameters(stage: 0, type: SearchType.Normal, depth: 9),
                                              new SearchParameters(stage: 44, type: SearchType.Normal, depth: 64)},
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
                Params = new[] { new SearchParameters(stage: 0, type: SearchType.Normal, depth: 9),
                                              new SearchParameters(stage: 40, type: SearchType.Normal, depth: 64)},
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

        static void StartGame()
        {
            Board board;

            Evaluator evaluator = new EvaluatorPatternBased_Release();
            PlayerAI p = new PlayerAI(evaluator)
            {
                Params = new[] { new SearchParameters(stage: 0, type: SearchType.Normal, depth: 9),
                                              new SearchParameters(stage: 44, type: SearchType.Normal, depth: 64)},
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
