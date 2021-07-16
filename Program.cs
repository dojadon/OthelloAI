using OthelloAI.Patterns;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OthelloAI
{
    static class Program
    {
        public static readonly Pattern PATTERN_EDGE2X = new Pattern("e_edge_x.dat", PatternType.X_SYMETRIC, 10, 0b0100001011111111UL);
        public static readonly Pattern PATTERN_EDGE_BLOCK = new Pattern("e_edge_block.dat", PatternType.X_SYMETRIC, 10, 0b0100001011111111UL);
        public static readonly Pattern PATTERN_CORNER_BLOCK = new Pattern("e_corner_block.dat", PatternType.XY_SYMETRIC, 9, 0b0100001011111111UL);
        public static readonly Pattern PATTERN_CORNER = new Pattern("e_corner.dat", PatternType.XY_SYMETRIC, 10, 0b0100001011111111UL);
        public static readonly Pattern PATTERN_LINE1 = new Pattern("e_line1.dat", PatternType.X_SYMETRIC, 8, 0b11111111UL << 8);
        public static readonly Pattern PATTERN_LINE2 = new Pattern("e_line2.dat", PatternType.X_SYMETRIC, 8, 0b11111111UL << 16);
        public static readonly Pattern PATTERN_LINE3 = new Pattern("e_line3.dat", PatternType.X_SYMETRIC, 8, 0b11111111UL << 24);
        public static readonly Pattern PATTERN_DIAGONAL8 = new Pattern("e_diag8.dat", PatternType.DIAGONAL, 8, 0x8040201008040201UL);
        public static readonly Pattern PATTERN_DIAGONAL7 = new Pattern("e_diag7.dat", PatternType.XY_SYMETRIC, 7, 0x1020408102040UL);
        public static readonly Pattern PATTERN_DIAGONAL6 = new Pattern("e_diag6.dat", PatternType.XY_SYMETRIC, 6, 0x10204081020UL);
        public static readonly Pattern PATTERN_DIAGONAL5 = new Pattern("e_diag5.dat", PatternType.XY_SYMETRIC, 5, 0x102040810UL);

        public static readonly Pattern[] PATTERNS = { PATTERN_EDGE2X, PATTERN_EDGE_BLOCK, PATTERN_CORNER_BLOCK, PATTERN_CORNER,
            PATTERN_LINE1, PATTERN_LINE2, PATTERN_LINE3, PATTERN_DIAGONAL8, PATTERN_DIAGONAL7, PATTERN_DIAGONAL6,
            PATTERN_DIAGONAL5 };

        static void Main()
        {
            ulong b = 0b00000001_00000010_00000100_00001000_00010000UL;
            new Board(b, 0).print();
            new Board(b, 0).Transposed().print();

            Pattern.InitTable();

            foreach (Pattern p in PATTERNS)
            {
                // Console.WriteLine(p.Test());
                p.Init();
                p.Load();
                Console.WriteLine(p);
            }

           //PATTERN_EDGE2X.Info(30, 1F);

           //var builder = new PatternEvaluationBuilder(PATTERNS);
          // builder.Load(@"C:\Users\zyand\eclipse-workspace\tus\Report7\log\log1.dat");
           //builder.Load(@"C:\Users\zyand\eclipse-workspace\tus\Report7\log\log2.dat");

            //StartClient();
            StartGame();
        }

        static void StartClient()
        {
            Evaluator evaluator = new EvaluatorPatternBased();
            PlayerNegascout p = new PlayerNegascout(evaluator)
            {
                SearchDepth = 9,
                DepthDoMoveOrdering = 6,
                ShallowSearchDepth = 0,
                StoneCountDoFullSearch = 46
            };

            Client client = new Client(p);
            client.Run("localhost", 25033, "Gen2");
        }

        static bool Step(ref Board board, Player player, int stone)
        {
            Move move = player.DecideMove(board, stone);
            if (move.move != 0)
            {
                board = board.Reversed(move.move, stone);
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
                ShallowSearchDepth = 0,
                StoneCountDoFullSearch = 60
            };

            for (int i = 0; i < 1; i++)
            {
                board = new Board(Board.InitB, Board.InitW, 4);

                while (Step(ref board, p, 1) | Step(ref board, p, -1))
                {
                }

                Console.WriteLine(i);
            }

            for(int i = 0; i < p.time.Length; i++)
            {
                if(p.count[i] > 0)
                {
                    Console.WriteLine(1000.0 * p.time[i] / p.count[i] / System.Diagnostics.Stopwatch.Frequency);
                    Console.WriteLine();
                }
            }
        }
    }
}
