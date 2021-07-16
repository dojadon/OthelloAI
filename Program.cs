using OthelloAI.Patterns;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OthelloAI
{
    static class Program
    {
        public static readonly Pattern PATTERN_EDGE2X = new PatternEdge("e_edge_x.dat");
        public static readonly Pattern PATTERN_EDGE_BLOCK = new PatternEdgeBlock("e_edge_block.dat");
        public static readonly Pattern PATTERN_CORNER_BLOCK = new PatternCornerBlock("e_corner_block.dat");
        public static readonly Pattern PATTERN_CORNER = new PatternCorner("e_corner.dat");
        public static readonly Pattern PATTERN_LINE1 = new PatternLine("e_line1.dat", 1);
        public static readonly Pattern PATTERN_LINE2 = new PatternLine("e_line2.dat", 2);
        public static readonly Pattern PATTERN_LINE3 = new PatternLine("e_line3.dat", 3);
        public static readonly Pattern PATTERN_DIAGONAL8 = new PatternDiagonal8("e_diag8.dat");
        public static readonly Pattern PATTERN_DIAGONAL7 = new PatternDiagonalN("e_diag7.dat", 7);
        public static readonly Pattern PATTERN_DIAGONAL6 = new PatternDiagonalN("e_diag6.dat", 6);
        public static readonly Pattern PATTERN_DIAGONAL5 = new PatternDiagonalN("e_diag5.dat", 5);
        public static readonly Pattern PATTERN_DIAGONAL4 = new PatternDiagonalN("e_diag4.dat", 4);

        public static readonly Pattern[] PATTERNS = { PATTERN_EDGE2X, PATTERN_EDGE_BLOCK, PATTERN_CORNER_BLOCK, PATTERN_CORNER,
            PATTERN_LINE1, PATTERN_LINE2, PATTERN_LINE3, PATTERN_DIAGONAL8, PATTERN_DIAGONAL7, PATTERN_DIAGONAL6,
            PATTERN_DIAGONAL5, PATTERN_DIAGONAL4 };

        static void Main()
        {
            Pattern.InitTable();
            //ulong b = 0b00111110_00000010;

            foreach (Pattern p in PATTERNS)
            {
                //Console.WriteLine(p.Test());
                p.Init();
                p.Load();
                Console.WriteLine(p);
            }

            //PATTERN_EDGE2X.Info(30, 0.1F);

           // var builder = new PatternEvaluationBuilder(PATTERNS);
           // builder.Load(@"C:\Users\zyand\eclipse-workspace\tus\Report7\log\log0.dat");

           //StartClient();
           StartGame();
        }

        static void StartClient()
        {
            Evaluator evaluator = new EvaluatorPatternBased();
            PlayerNegascout p = new PlayerNegascout(evaluator)
            {
                SearchDepth = 11,
                DepthDoMoveOrdering = 8,
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
