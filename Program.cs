﻿using OthelloAI.Patterns;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OthelloAI
{
    static class Program
    {
        public static readonly Pattern PATTERN_EDGE = new PatternVerticalLine0("e_edge.dat", PatternType.X_SYMETRIC);
        public static readonly Pattern PATTERN_CORNER_SMALL = new PatternBitMask("e_corner_small.dat", PatternType.XY_SYMETRIC, 8, 0b00000111_00000111_00000011UL);

        public static readonly Pattern PATTERN_EDGE2X = new PatternEdge2X("e_edge_x.dat", PatternType.X_SYMETRIC);
        public static readonly Pattern PATTERN_EDGE_BLOCK = new PatternBitMask("e_edge_block.dat", PatternType.X_SYMETRIC, 10, 0b00111100_10111101UL);
        public static readonly Pattern PATTERN_CORNER_BLOCK = new PatternBitMask("e_corner_block.dat", PatternType.XY_SYMETRIC, 9, 0b00000111_00000111_00000111UL);
        public static readonly Pattern PATTERN_CORNER = new PatternBitMask("e_corner.dat", PatternType.XY_SYMETRIC, 10, 0b10000000_10000000_10000000_00000011_00011111UL);
        public static readonly Pattern PATTERN_LINE1 = new PatternVerticalLine1("e_line1.dat", PatternType.X_SYMETRIC);
        public static readonly Pattern PATTERN_LINE2 = new PatternVerticalLine2("e_line2.dat", PatternType.X_SYMETRIC);
        public static readonly Pattern PATTERN_LINE3 = new PatternVerticalLine3("e_line3.dat", PatternType.X_SYMETRIC);
        public static readonly Pattern PATTERN_DIAGONAL8 = new PatternBitMask("e_diag8.dat", PatternType.DIAGONAL, 8, 0x8040201008040201UL);
        public static readonly Pattern PATTERN_DIAGONAL7 = new PatternBitMask("e_diag7.dat", PatternType.XY_SYMETRIC, 7, 0x1020408102040UL);
        public static readonly Pattern PATTERN_DIAGONAL6 = new PatternBitMask("e_diag6.dat", PatternType.XY_SYMETRIC, 6, 0x10204081020UL);
        public static readonly Pattern PATTERN_DIAGONAL5 = new PatternBitMask("e_diag5.dat", PatternType.XY_SYMETRIC, 5, 0x102040810UL);

        public static readonly Pattern[] PATTERNS = { PATTERN_EDGE2X, PATTERN_EDGE_BLOCK, PATTERN_CORNER_BLOCK, PATTERN_CORNER,
            PATTERN_LINE1, PATTERN_LINE2, PATTERN_LINE3, PATTERN_DIAGONAL8, PATTERN_DIAGONAL7, PATTERN_DIAGONAL6,
            PATTERN_DIAGONAL5 };

        public static MPCParamSolver.MCP MCP_PARAM2;
        public static MPCParamSolver.MCP MCP_PARAM4;

        static void Main()
        {
            Console.WriteLine($"Support BMI2 : {System.Runtime.Intrinsics.X86.Bmi2.X64.IsSupported}");
            Console.WriteLine($"Support AVX2 : {System.Runtime.Intrinsics.X86.Avx2.IsSupported}");
            Console.WriteLine($"Support AVX : {System.Runtime.Intrinsics.X86.Avx.IsSupported}");

            foreach (Pattern p in PATTERNS)
            {
                p.Init();
                p.Load();
                Console.WriteLine(p);
                Console.WriteLine(p.Test());
            }

            var solver = MPCParamSolver.FromFile("data.dat");
            MCP_PARAM2 = solver.SolveParameters(2, 12, 50);
            MCP_PARAM4 = solver.SolveParameters(4, 12, 50);

            // var builder = new PatternEvaluationBuilder(new Pattern[] { PATTERN_EDGE, PATTERN_CORNER_SMALL });
            // builder.Load(@"C:\Users\zyand\eclipse-workspace\tus\Report7\log\log1.dat");
            // builder.Load(@"C:\Users\zyand\eclipse-workspace\tus\Report7\log\log2.dat");

             // MPCParamSolver.Test();
            // StartUpdataEvaluation();
           // StartClient();
            // TestFFO();
             StartGame(); 
            // StartManualGame();
            // UpdataEvaluationWithDatabase();
        }

        static void StartUpdataEvaluation()
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

            Evaluator evaluator = new EvaluatorPatternBased();
            PlayerAI p = new PlayerAI(new EvaluatorRandomize(evaluator, 50))
            {
                ParamBeg = new SearchParameters(depth: 7, stage: 0, new CutoffParameters(true, true, false)),
                ParamMid = new SearchParameters(depth: 7, stage: 16, new CutoffParameters(true, true, false)),
                ParamEnd = new SearchParameters(depth: 64, stage: 46, new CutoffParameters(true, true, false)),
                PrintInfo = false,
            };

            for (int i = 0; i < 10000; i++)
            {
                Board board = new Board(Board.InitB, Board.InitW, 4);
                List<Board> boards = new List<Board>();

                while (Step(ref board, boards, p, 1) | Step(ref board, boards, p, -1))
                {
                }

                int result = board.GetStoneCountGap();
                foreach (var b in boards)
                {
                    UpdataEvaluation(b, result, 0.001F);
                }

                //if(i % 10 == 0)
                {
                    Console.WriteLine($"{i}, B: {board.GetStoneCount(1)}, W: {board.GetStoneCount(-1)}");
                }

                if (i % 10 == 0)
                {
                    Array.ForEach(PATTERNS, p => p.Save());
                }
            }
        }

        static void UpdataEvaluation(Board board, int result, float alpha)
        {
            var boards = new MirroredBoards(board);
            MirroredNeededBoards.Create(board, out Board b1, out Board b2, out Board b3, out Board b4);

            int stage = board.n_stone - 5;
            int move_gap = Board.BitCount(board.GetMoves()) - Board.BitCount(board.GetOpponentMoves());

            float e = result - PATTERNS.Sum(p => p.EvalForTraining(board, b1, b2, b3, b4)) - move_gap;
            Array.ForEach(PATTERNS, p => Array.ForEach(boards.Boards, b => p.UpdataEvaluation(b, e * alpha)));
        }

        static void UpdataEvaluationWithDatabase()
        {
            var reader = new WthorRecordReader();
            reader.OnLoadMove += (board, result) => UpdataEvaluation(board, result, 0.005F);

            for (int k = 0; k < 2; k++)
            {
                for (int i = 2001; i <= 2015; i++)
                {
                    Console.WriteLine($"{k} : WTH/WTH_{i}.wtb");
                    reader.Load($"WTH/WTH_{i}.wtb");
                    Array.ForEach(PATTERNS, p => p.Save());
                }
            }
        }

        static void StartClient()
        {
            Evaluator evaluator = new EvaluatorPatternBased();
            PlayerAI p = new PlayerAI(evaluator)
            {
                ParamBeg = new SearchParameters(depth: 13, stage: 0, new CutoffParameters(true, true, false)),
                ParamMid = new SearchParameters(depth: 13, stage: 10, new CutoffParameters(true, true, false)),
                ParamEnd = new SearchParameters(depth: 64, stage: 40, new CutoffParameters(true, true, false)),
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
            Evaluator evaluator = new EvaluatorPatternBased();
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

            Evaluator evaluator = new EvaluatorPatternBased();
            PlayerAI p = new PlayerAI(evaluator)
            {
                ParamBeg = new SearchParameters(depth: 12, stage: 0, new CutoffParameters(true, true, false)),
                ParamMid = new SearchParameters(depth: 12, stage: 16, new CutoffParameters(true, true, false)),
                ParamEnd = new SearchParameters(depth: 64, stage: 44, new CutoffParameters(true, true, false)),
            };

            for (int i = 0; i < 1; i++)
            {
                board = new Board(Board.InitB, Board.InitW);
                board = board.Reversed(Board.Mask(2, 3)).Reversed(Board.Mask(4, 2));

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
            Evaluator evaluator = new EvaluatorPatternBased();
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
