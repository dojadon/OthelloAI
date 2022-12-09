using System;
using System.IO;
using System.Linq;
using System.Threading;
using NumSharp;
using OthelloAI.GA;

namespace OthelloAI
{
    public static class Program
    {
        private static ThreadLocal<Random> ThreadLocalRandom { get; } = new ThreadLocal<Random>(() => new Random());
        public static Random Random => ThreadLocalRandom.Value;

        public static readonly Weight WEIGHT_EDGE2X = new WeightsArrayR(0b01000010_11111111UL);
        public static readonly Weight WEIGHT_EDGE_BLOCK = new WeightsArrayR(0b00000111_00000111_00000111UL);
        public static readonly Weight WEIGHT_CORNER_BLOCK = new WeightsArrayR(0b00000001_00000001_00000001_00000011_00011111UL);
        public static readonly Weight WEIGHT_CORNER = new WeightsArrayR(0b01000010_11111111UL);
        public static readonly Weight WEIGHT_LINE1 = new WeightsArrayR(0b11111111UL);
        public static readonly Weight WEIGHT_LINE2 = new WeightsArrayR(0b11111111_00000000UL);

        public static readonly Weight WEIGHT = new WeightsSum(WEIGHT_EDGE2X, WEIGHT_EDGE_BLOCK, WEIGHT_CORNER_BLOCK, WEIGHT_CORNER, WEIGHT_LINE1, WEIGHT_LINE2);

        static void Main()
        {
            //GATest.TestES();

            Tester.TestGAResult();
            return;

            Tester.TestError2();
            return;

            WEIGHT.Load("e.dat");

            var t = Tester.TestError(WEIGHT, new [] { 4F, 6F }, 0, 1000);
            var a = np.array(t).mean(0);

            var log = $"G:/マイドライブ/Lab/test/log_s_err_{DateTime.Now:yyyy_MM_dd_HH_mm}.csv";
            using StreamWriter sw = File.AppendText(log);

            for (int i = 0; i < a.shape[0]; i++)
            {
                Console.WriteLine(string.Join(", ", a[i].ToArray<float>()));
                sw.WriteLine(string.Join(", ", a[i].ToArray<float>()));
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
                Board board = Tester.PlayGame(p, p, Board.Init, r => Console.WriteLine(r.next_board));

                Console.WriteLine($"B: {board.GetStoneCount(1)}");
                Console.WriteLine($"W: {board.GetStoneCount(-1)}");
                Console.WriteLine(i);
            }
        }
    }
}
