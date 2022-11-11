using System;
using System.IO;
using System.Linq;
using System.Threading;
using NumSharp;

namespace OthelloAI
{
    public static class Program
    {
        private static ThreadLocal<Random> ThreadLocalRandom { get; } = new ThreadLocal<Random>(() => new Random());
        public static Random Random => ThreadLocalRandom.Value;

        public const int NUM_STAGES = 1;
        public const bool LOAD = false;

        public static readonly Weight WEIGHT_EDGE2X = Weight.Create(new BoardHasherMask(0b01000010_11111111UL), NUM_STAGES);
        public static readonly Weight WEIGHT_EDGE_BLOCK = Weight.Create(new BoardHasherMask(0b00111100_10111101UL), NUM_STAGES);
        public static readonly Weight WEIGHT_CORNER_BLOCK = Weight.Create(new BoardHasherMask(0b00000111_00000111_00000111UL), NUM_STAGES);
        public static readonly Weight WEIGHT_CORNER = Weight.Create(new BoardHasherMask(0b00000001_00000001_00000001_00000011_00011111UL), NUM_STAGES);
        public static readonly Weight WEIGHT_LINE1 = Weight.Create(new BoardHasherLine1(1), NUM_STAGES);
        public static readonly Weight WEIGHT_LINE2 = Weight.Create(new BoardHasherLine1(2), NUM_STAGES);

        public static readonly Weight WEIGHT = new WeightsSum(WEIGHT_EDGE2X, WEIGHT_EDGE_BLOCK, WEIGHT_CORNER_BLOCK, WEIGHT_CORNER, WEIGHT_LINE1, WEIGHT_LINE2);

        static void Main()
        {
            WEIGHT.Load("e.dat");

            var log = $"test/log_test_error_{DateTime.Now:yyyy_MM_dd_HH_mm}.csv";
            using StreamWriter sw = File.AppendText(log);

            var e3 = Tester.TestError(WEIGHT, Enumerable.Range(0, 4).Select(i => i * 2F), 10000);
            var e = np.array(e3).mean(0);

            for (int i = 0; i < e.shape[0]; i++)
            {
                Console.WriteLine(e[i].ToString());
                sw.WriteLine(string.Join(", ", e[i].ToArray<float>()));
            }
        }

        static void StartClient(Evaluator evaluator)
        {
            PlayerAI p = new PlayerAI(evaluator)
            {
                Params = new[] { new SearchParameters(stage: 0, type: SearchType.Normal, depth: 9),
                                              new SearchParameters(stage: 44, type: SearchType.Normal, depth: 64)},
            };

            Client client = new Client(p);
            client.Run("localhost", 25033, "Gen2");
        }

        static void StartGame(Evaluator evaluator)
        {
            PlayerAI p = new PlayerAI(evaluator)
            {
                Params = new[] { new SearchParameters(stage: 0, type: SearchType.IterativeDeepening, depth: 11),
                new SearchParameters(stage: 44, type: SearchType.Normal, depth: 64) },
                PrintInfo = true
            };

            for (int i = 0; i < 1; i++)
            {
                Board board = Tester.PlayGame(p, p, Board.Init, (b, c, m, e) => Console.WriteLine(b.Reversed(m, c)));

                Console.WriteLine($"B: {board.GetStoneCount(1)}");
                Console.WriteLine($"W: {board.GetStoneCount(-1)}");
                Console.WriteLine(i);
            }
        }
    }
}
