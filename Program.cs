using NumSharp;
using OthelloAI.GA;
using System;
using System.IO;
using System.Threading;

namespace OthelloAI
{
    public static class Program
    {
        public const int NumThreads = 24;

        private static ThreadLocal<Random> ThreadLocalRandom { get; } = new ThreadLocal<Random>(() => new Random());
        public static Random Random => ThreadLocalRandom.Value;

        public static readonly ulong[] MASKS = new[] {
            0b01000010_11111111U,
            0b00000111_00000111_00000111UL,
            0b00000001_00000001_00000001_00000011_00011111UL,
            0b01000010_11111111UL,
            0b11111111UL,
            0b11111111_00000000UL,
            0b11111111_00000000_00000000UL,
            0x8040201008040201UL,
            0x1020408102040UL,
            0x10204081020UL,
            0x102040810UL,
        };

        public static readonly Weight WEIGHT_EDGE2X = new WeightsArrayR(0b01000010_11111111UL);
        public static readonly Weight WEIGHT_EDGE_BLOCK = new WeightsArrayR(0b00000111_00000111_00000111UL);
        public static readonly Weight WEIGHT_CORNER_BLOCK = new WeightsArrayR(0b00000001_00000001_00000001_00000011_00011111UL);
        public static readonly Weight WEIGHT_CORNER = new WeightsArrayR(0b01000010_11111111UL);
        public static readonly Weight WEIGHT_LINE1 = new WeightsArrayR(0b11111111UL);
        public static readonly Weight WEIGHT_LINE2 = new WeightsArrayR(0b11111111_00000000UL);

        public static readonly Weight WEIGHT = new WeightsSum(WEIGHT_EDGE2X, WEIGHT_EDGE_BLOCK, WEIGHT_CORNER_BLOCK, WEIGHT_CORNER, WEIGHT_LINE1, WEIGHT_LINE2);

        static void Main()
        {
            GATest.TestBRKGA();

            Tester.TestWeights();

            //Tester.TestError3();
        }

        static void StartClient(Evaluator evaluator)
        {
            PlayerAI p = new PlayerAI(evaluator)
            {
                Params = new[] { new SearchParameterFactory(stage: 0, type: SearchType.Normal, depth: 9),
                                              new SearchParameterFactory(stage: 44, type: SearchType.Normal, depth: 64)},
            };

            Client client = new Client(p);
            client.Run("localhost", 25033, "Gen2");
        }

        static void StartGame(Evaluator evaluator)
        {
            PlayerAI p = new PlayerAI(evaluator)
            {
                Params = new[] { new SearchParameterFactory(stage: 0, type: SearchType.IterativeDeepening, depth: 11),
                new SearchParameterFactory(stage: 44, type: SearchType.Normal, depth: 64) },
                PrintInfo = true
            };

            for (int i = 0; i < 1; i++)
            {
                Board board = Tester.PlayGame(p, p, Board.Init, r => Console.WriteLine(r.next_board));

                Console.WriteLine($"B: {board.GetStoneCount(1)}");
                Console.WriteLine($"W: {board.GetStoneCount(-1)}");
                Console.WriteLine(i);
            }
        }
    }
}
