using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;

namespace OthelloAI
{
    public class MPCParamSolver
    {
        public static bool Step(List<MPCParamSolver> solvers, ref Board board, Player player, int stone)
        {
            (_, _, ulong move) = player.DecideMove(board, stone);
            if (move != 0)
            {
                Board b = stone == 1 ? board : board.ColorFliped();
                solvers.ForEach(s => s.StackData(b));

                board = board.Reversed(move, stone);
                // board.print();

                return true;
            }
            return false;
        }


        public static void Test()
        {
            PlayerNegascout player = new PlayerNegascout(new EvaluatorRandomize(new EvaluatorPatternBased(), 30))
            {
                ParamBeg = new SearchParameters(depth: 9, stage: 0, new CutoffParameters(true, true, false)),
                ParamMid = new SearchParameters(depth: 12, stage: 16, new CutoffParameters(true, true, true)),
                ParamEnd = new SearchParameters(depth: 64, stage: 42, new CutoffParameters(true, true, false)),
                PrintInfo = false,
            };

            var list = new List<MPCParamSolver>
            {
                new MPCParamSolver(player, 2, 5),
                new MPCParamSolver(player, 3, 6),
                new MPCParamSolver(player, 4, 7),
                //new MPCParamSolver(player, 5, 8),
            };

            for (int i = 0; i < 100; i++)
            {
                Board board = new Board(Board.InitB, Board.InitW, 4);

                while (board.stoneCount < 52 && (Step(list, ref board, player, 1) | Step(list, ref board, player, -1)))
                {

                }

                Console.WriteLine($"Game : {i}");
                Console.WriteLine($"    B : {board.GetStoneCount(1)}");
                Console.WriteLine($"    W : {board.GetStoneCount(-1)}");
                Console.WriteLine();
            }

            foreach (var s in list)
            {
                Console.WriteLine($"{s.Depth1}/{s.Depth2}");
                for (int i = 0; i < 60; i++)
                {
                    if (s.HasData(i))
                        s.CalcSigma(i);
                }
                Console.WriteLine();
            }

            foreach (var s in list)
            {
                s.Export();
            }
        }

        public static (float, float) LinearRegression(List<float> x, List<float> y)
        {
            int n = x.Count;
            float ax = x.Average();
            float ay = y.Average();

            float a = x.Zip(y, (xi, yi) => (xi - ax) * (yi - ay)).Sum() / x.Select(x => (x - ax) * (x - ax)).Sum();

            return (a, ay - a * ax);
        }

        PlayerNegascout Player { get; }
        int Depth1 { get; }
        int Depth2 { get; }

        public MPCParamSolver(PlayerNegascout player, int depth1, int depth2)
        {
            Player = player;
            Depth1 = depth1;
            Depth2 = depth2;
        }

        List<float>[] X { get; } = Enumerable.Range(0, 60).Select(i => new List<float>()).ToArray();
        List<float>[] Y { get; } = Enumerable.Range(0, 60).Select(i => new List<float>()).ToArray();

        public void StackData(Board board)
        {
            foreach(Move m in new Move(board).NextMoves())
            {
                X[board.stoneCount - 4].Add(Player.Search(new Dictionary<Board, (float, float)>(), m, new CutoffParameters(true, true, false), Depth1, -1000000, 1000000));
                Y[board.stoneCount - 4].Add(Player.Search(new Dictionary<Board, (float, float)>(), m, new CutoffParameters(true, true, false), Depth2, -1000000, 1000000));
            }
        }

        public bool HasData(int stage)
        {
            return X[stage].Count > 0;
        }

        public float CalcSigma(int stage)
        {
            (float a, float b) = LinearRegression(X[stage], Y[stage]);
            float sigma = (float)Math.Sqrt(X[stage].Zip(Y[stage], (x, y) => Math.Pow(y - a * x - b, 2)).Sum() / X[stage].Count);
            Console.WriteLine($"{a}, {b}, {sigma}");
            return (float)Math.Sqrt(X[stage].Zip(Y[stage], (x, y) => Math.Pow(y - a * x - b, 2)).Sum() / X[stage].Count);
        }

        public void Export()
        {
            File.WriteAllText($"{Depth1}_{Depth2}.txt", string.Join("\r\n", X.Zip(Y, (x, y) => x.Count == 0 ? "" : string.Join(",,",  x.Zip(y, (xi, yi) => $"{xi}, {yi}") ))));
        }
    }
}
