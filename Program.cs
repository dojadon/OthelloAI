using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace OthelloAI
{
    static class Program
    {
        public const int NUM_STAGES = 10;

        public static readonly Pattern PATTERN_EDGE2X = new Pattern("e_edge_x.dat", NUM_STAGES, new BoardHasherMask(0b01000010_11111111UL), PatternType.X_SYMMETRIC);
        public static readonly Pattern PATTERN_EDGE_BLOCK = new Pattern("e_edge_block.dat", NUM_STAGES, new BoardHasherMask(0b00111100_10111101UL), PatternType.X_SYMMETRIC);
        public static readonly Pattern PATTERN_CORNER_BLOCK = new Pattern("e_corner_block.dat", NUM_STAGES, new BoardHasherMask(0b00000111_00000111_00000111UL), PatternType.XY_SYMMETRIC);
        public static readonly Pattern PATTERN_CORNER = new Pattern("e_corner.dat", NUM_STAGES, new BoardHasherMask(0b00000001_00000001_00000001_00000011_00011111UL), PatternType.XY_SYMMETRIC);
        public static readonly Pattern PATTERN_LINE1 = new Pattern("e_line1.dat", NUM_STAGES, new BoardHasherLine1(1), PatternType.X_SYMMETRIC);
        public static readonly Pattern PATTERN_LINE2 = new Pattern("e_line2.dat", NUM_STAGES, new BoardHasherLine1(2), PatternType.X_SYMMETRIC);
        public static readonly Pattern PATTERN_LINE3 = new Pattern("e_line3.dat", NUM_STAGES, new BoardHasherLine1(3), PatternType.X_SYMMETRIC);
        public static readonly Pattern PATTERN_DIAGONAL8 = new Pattern("e_diag8.dat", NUM_STAGES, new BoardHasherMask(0x8040201008040201UL), PatternType.DIAGONAL);
        public static readonly Pattern PATTERN_DIAGONAL7 = new Pattern("e_diag7.dat", NUM_STAGES, new BoardHasherMask(0x1020408102040UL), PatternType.XY_SYMMETRIC);
        public static readonly Pattern PATTERN_DIAGONAL6 = new Pattern("e_diag6.dat", NUM_STAGES, new BoardHasherMask(0x10204081020UL), PatternType.XY_SYMMETRIC);
        public static readonly Pattern PATTERN_DIAGONAL5 = new Pattern("e_diag5.dat", NUM_STAGES, new BoardHasherMask(0x102040810UL), PatternType.XY_SYMMETRIC);

        public static readonly Pattern[] PATTERNS = { PATTERN_EDGE2X, PATTERN_EDGE_BLOCK, PATTERN_CORNER_BLOCK, PATTERN_CORNER,
            PATTERN_LINE1, PATTERN_LINE2, PATTERN_LINE3, PATTERN_DIAGONAL8, PATTERN_DIAGONAL7, PATTERN_DIAGONAL6, PATTERN_DIAGONAL5 };

        static void Main()
        {
            Test2();
            return;
            GA.GATest.TestBRKGA();
            // Tester.Test1();
            return;

            Console.WriteLine($"Support BMI2 : {System.Runtime.Intrinsics.X86.Bmi2.X64.IsSupported}");
            Console.WriteLine($"Support AVX2 : {System.Runtime.Intrinsics.X86.Avx2.IsSupported}");
            Console.WriteLine($"Support AVX : {System.Runtime.Intrinsics.X86.Avx.IsSupported}");

            Console.WriteLine();
#if BIN_HASH
            Console.WriteLine("Pattern Hashing Type : Bin");
#elif TER_HASH
            Console.WriteLine("Pattern Hashing Type : Ter");
#else
            Console.WriteLine("Pattern Hashing Type : Undefined");
#endif
            Console.WriteLine();

            foreach (Pattern p in PATTERNS)
            {
                p.Load();
                Console.WriteLine(p);
                Console.WriteLine(p.Test());
            }

            // PATTERN_EDGE2X.Info(32, 2F);

            // Tester.Test1();
            // GA.GA<float[], GA.Score<float[]>>.TestBRKGA();
            Test();
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

        static void Test2()
        {
            string[] s = "330697	344967	116611	14447	70585	395203	329487	276724	262399	132703	312259	156047".Split();

            Pattern[] p1 = s.Select(t => new Pattern($"p{t}.dat", 10, new BoardHasherMask(ulong.Parse(t)), PatternType.ASYMMETRIC)).ToArray();

            //Pattern[] p1 =  { new Pattern("p11.dat", 10, new BoardHasherMask(0b01110000_11111011UL), PatternType.ASYMMETRIC),
            //                        new Pattern("p12.dat", 10, new BoardHasherMask(0b10100000_11000011_11000011UL), PatternType.ASYMMETRIC),
            //                        new Pattern("p13.dat", 10, new BoardHasherMask(0b01100000_11100010_11000011UL), PatternType.ASYMMETRIC),
            //                        new Pattern("p14.dat", 10, new BoardHasherMask(0b11100000_11100000_11100001UL), PatternType.ASYMMETRIC),
            //};

            Pattern[] p2 = PATTERNS;

            foreach (var p in p1)
                p.Load();

            foreach (var p in p2)
                p.Load();

            PlayerAI Create(Evaluator e)
            {
                return new PlayerAI(e)
                {
                    ParamBeg = new SearchParameters(depth: 3, stage: 0, new CutoffParameters(true, true, false)),
                    ParamMid = new SearchParameters(depth: 3, stage: 16, new CutoffParameters(true, true, false)),
                    ParamEnd = new SearchParameters(depth: 64, stage: 48, new CutoffParameters(true, true, false)),
                    PrintInfo = false,
                };
            }

            Random rand = new Random();

            int Play(PlayerAI p1, PlayerAI p2)
            {
                static bool Step(ref Board board, Player player, int stone)
                {
                    (int x, int y, ulong move) = player.DecideMove(board, stone);
                    if (move != 0)
                    {
                        board = board.Reversed(move, stone);
                        // Console.WriteLine(board);
                        return true;
                    }
                    return false;
                }

                Board board = Tester.CreateRnadomGame(rand, 8);
                while (Step(ref board, p1, 1) | Step(ref board, p2, -1))
                {
                }

                return board.GetStoneCountGap();
            }

            PlayerAI player1 = Create(new EvaluatorPatternBased(p1));
            PlayerAI player2 = Create(new EvaluatorPatternBased(p2));

            int w1 = 0;
            int w2 = 0;

            for (int i = 0; i < 1000; i++)
            {
                int result = Play(player1, player2);

                if (result > 0)
                    w1++;
                else if (result < 0)
                    w2++;

                Console.WriteLine($"{w1}, {w2}");
            }
        }

        static void Test()
        {
            string[] s = "330697	344967	116611	14447	70585	395203	329487	276724	262399	132703	312259	156047".Split();

            Pattern[] p1 = s.Select(t => new Pattern($"p{t}.dat", 10, new BoardHasherMask(ulong.Parse(t)), PatternType.ASYMMETRIC)).ToArray();

            //Pattern[] p1 = { new Pattern("p11.dat", 10, new BoardHasherMask(0b01110000_11111011UL), PatternType.ASYMMETRIC),
            //                        new Pattern("p12.dat", 10, new BoardHasherMask(0b10100000_11000011_11000011UL), PatternType.ASYMMETRIC),
            //                        new Pattern("p13.dat", 10, new BoardHasherMask(0b01100000_11100010_11000011UL), PatternType.ASYMMETRIC),
            //                        new Pattern("p14.dat", 10, new BoardHasherMask(0b11100000_11100000_11100001UL), PatternType.ASYMMETRIC),
            //};

            //Pattern[] p2 = { new Pattern("p21.dat", 10, new BoardHasherMask(0b11111111UL), PatternType.ASYMMETRIC),
            //                        new Pattern("p22.dat", 10, new BoardHasherMask(0b11000000_11100000_11100000UL), PatternType.ASYMMETRIC),
            //                        new Pattern("p23.dat", 10, new BoardHasherMask(0b10000000_11100000_11110000UL), PatternType.ASYMMETRIC),
            //};

            Pattern[] p2 = PATTERNS;

            PatternTrainer[] trainers = { new PatternTrainer(p1, 0.0025F), new PatternTrainer(p2, 0.0025F) };
            (Evaluator, float)[] evaluators = { (new EvaluatorPatternBased(p1), 0.5F), (new EvaluatorPatternBased(p2), 0.5F) };

            var evaluator = new EvaluatorBiasedRandomChoice(evaluators);

            PlayerAI player = new PlayerAI(evaluator)
            {
                ParamBeg = new SearchParameters(depth: 3, stage: 0, new CutoffParameters(true, true, false)),
                ParamMid = new SearchParameters(depth: 3, stage: 16, new CutoffParameters(true, true, false)),
                ParamEnd = new SearchParameters(depth: 64, stage: 52, new CutoffParameters(true, true, false)),
                PrintInfo = false,
            };

            for (int i = 0; i < 10000; i++)
            {
                var data = TrainerUtil.PlayForTrainingParallel(16, player);

                foreach (var trainer in trainers)
                {
                    foreach (var d in data)
                        trainer.Update(d.board, d.result);

                    Console.Write(trainer.Log.TakeLast(10000).Average() + ", ");
                }

                Console.WriteLine();

                if (i % 50 == 0)
                {
                    foreach (var p in p1)
                        p.Save();

                    foreach (var p in p2)
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
                ParamBeg = new SearchParameters(depth: 6, stage: 0, new CutoffParameters(true, true, false)),
                ParamMid = new SearchParameters(depth: 6, stage: 16, new CutoffParameters(true, true, false)),
                ParamEnd = new SearchParameters(depth: 64, stage: 48, new CutoffParameters(true, true, false)),
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
                ParamBeg = new SearchParameters(depth: 9, stage: 0, new CutoffParameters(true, true, false)),
                ParamMid = new SearchParameters(depth: 9, stage: 10, new CutoffParameters(true, true, false)),
                ParamEnd = new SearchParameters(depth: 64, stage: 44, new CutoffParameters(true, true, false)),
            };

            Client client = new Client(p);
            client.Run("localhost", 25033, "Gen2");
        }

        public static bool Step(ref Board board, Player player, int stone, bool print)
        {
            (_, _, ulong move) = player.DecideMove(board, stone);
            if (move != 0)
            {
                board = board.Reversed(move, stone);
                if (print)
                    Console.WriteLine(board);
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
                ParamBeg = new SearchParameters(depth: 9, stage: 0, new CutoffParameters(true, true, false)),
                ParamMid = new SearchParameters(depth: 9, stage: 16, new CutoffParameters(true, true, true)),
                ParamEnd = new SearchParameters(depth: 64, stage: 40, new CutoffParameters(true, true, false)),
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

                p.SolveEndGame(new Search(), board, p.ParamEnd.cutoff_param);
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
                ParamBeg = new SearchParameters(depth: 11, stage: 0, new CutoffParameters(true, true, false)),
                ParamMid = new SearchParameters(depth: 11, stage: 16, new CutoffParameters(true, true, false)),
                ParamEnd = new SearchParameters(depth: 64, stage: 44, new CutoffParameters(true, true, false)),
            };

            for (int i = 0; i < 1; i++)
            {
                board = new Board(Board.InitB, Board.InitW);
                //board = board.Reversed(Board.Mask(2, 3)).Reversed(Board.Mask(4, 2));

                while (Step(ref board, p, 1, true) | Step(ref board, p, -1, true))
                {
                    GC.Collect();
                }

                Console.WriteLine($"B: {board.GetStoneCount(1)}");
                Console.WriteLine($"W: {board.GetStoneCount(-1)}");
                Console.WriteLine(i);
            }

            Console.WriteLine($"Average : {p.Times.Average()}");
            Console.WriteLine($"Min : {p.Times.Min()}");
            Console.WriteLine($"Max : {p.Times.Max()}");
            Console.WriteLine();
            Console.WriteLine(string.Join("\r\n", p.Times));
            Console.WriteLine();
        }

        static void StartManualGame()
        {
            Evaluator evaluator = new EvaluatorPatternBased_Release();
            Player p1 = new PlayerAI(evaluator)
            {
                ParamBeg = new SearchParameters(depth: 11, stage: 0, new CutoffParameters(true, true, false)),
                ParamMid = new SearchParameters(depth: 11, stage: 16, new CutoffParameters(true, true, false)),
                ParamEnd = new SearchParameters(depth: 64, stage: 40, new CutoffParameters(true, true, false)),
            };

            Player p2 = new PlayerManual();

            Board board = new Board();

            for (int i = 0; i < 1; i++)
            {
                board = new Board(Board.InitB, Board.InitW, 4);

                while (Step(ref board, p1, 1, true) | Step(ref board, p2, -1, true))
                {
                }
            }
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine($"B: {board.GetStoneCount(1)}");
            Console.WriteLine($"W: {board.GetStoneCount(-1)}");
        }
    }
}
