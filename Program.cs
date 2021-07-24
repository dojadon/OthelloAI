using OthelloAI.Patterns;
using System;
using System.Collections.Generic;
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

        public static readonly Pattern[] PATTERNS = { PATTERN_EDGE, PATTERN_CORNER_SMALL, PATTERN_EDGE2X, PATTERN_EDGE_BLOCK, PATTERN_CORNER_BLOCK, PATTERN_CORNER,
            PATTERN_LINE1, PATTERN_LINE2, PATTERN_LINE3, PATTERN_DIAGONAL8, PATTERN_DIAGONAL7, PATTERN_DIAGONAL6,
            PATTERN_DIAGONAL5 };

        static void Main()
        {
            Pattern.InitTable();

            foreach (Pattern p in PATTERNS)
            {
                Console.WriteLine(p.Test());
                p.Init();
                p.Load();
                Console.WriteLine(p);
            }


            //    var builder = new PatternEvaluationBuilder(new Pattern[] { PATTERN_EDGE, PATTERN_CORNER_SMALL });
            //   builder.Load(@"C:\Users\zyand\eclipse-workspace\tus\Report7\log\log1.dat");
            // builder.Load(@"C:\Users\zyand\eclipse-workspace\tus\Report7\log\log2.dat");

            // StartClient();
           StartGame();
            // StartManualGame();
        }

        static void StartClient()
        {
            Evaluator evaluator = new EvaluatorPatternBased();
            PlayerNegascout p = new PlayerNegascout(evaluator)
            {
                SearchDepth = 8,
                DepthDoMoveOrdering = 6,
                StoneCountDoFullSearch = 46
            };

            Client client = new Client(p);
            client.Run("localhost", 25033, "Gen2");
        }

        static bool Step(ref Board board, Player player, int stone)
        {
            (int x, int y, ulong move) = player.DecideMove(board, stone);
            if (move != 0)
            {
                board = board.Reversed(move, stone);
                board.print();
                return true;
            }
            return false;
        }

        static void StartGame()
        {
            Board board;

            Evaluator evaluator = new EvaluatorPatternBased();
            PlayerNegascout p = new PlayerNegascout(evaluator)
            {
                SearchDepth = 9,
                DepthDoMoveOrdering = 6,
                StoneCountDoFullSearch = 40
            };

            for (int i = 0; i < 1; i++)
            {
                board = new Board(Board.InitB, Board.InitW, 4);

                while (Step(ref board, p, 1) | Step(ref board, p, -1))
                {
                }

                Console.WriteLine($"B: {board.GetStoneCount(1)}");
                Console.WriteLine($"W: {board.GetStoneCount(-1)}");
                Console.WriteLine(i);
            }

            Console.WriteLine($"Average : {p.times.Average()}");
            Console.WriteLine($"Min : {p.times.Min()}");
            Console.WriteLine($"Max : {p.times.Max()}");
            Console.WriteLine();
            Console.WriteLine(string.Join("\r\n", p.times));
            Console.WriteLine();
        }

        static void StartManualGame()
        {
            Evaluator evaluator = new EvaluatorPatternBased();
            Player p1 = new PlayerNegascout(evaluator)
            {
                SearchDepth = 9,
                DepthDoMoveOrdering = 6,
                StoneCountDoFullSearch = 44
            };

            Player p2 = new PlayerManual();

            Board board = new Board();

            for (int i = 0; i < 1; i++)
            {
                board = new Board(Board.InitB, Board.InitW, 4);

                while (Step(ref board, p1, 1) | Step(ref board, p2, -1))
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
