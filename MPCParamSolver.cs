using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using System.Numerics;

namespace OthelloAI
{
    public class MPCParamSolver
    {
        public static bool Step(MPCParamSolver solver, ref Board board, PlayerAI player, int stone)
        {
            (_, _, ulong move) = player.DecideMove(board, stone);
            if (move != 0)
            {
                solver.StackData(player, board);
                board = board.Reversed(move, stone);
                Console.WriteLine(board);
                return true;
            }
            return false;
        }

        public static bool Step(ref Board board, Player player)
        {
            (_, _, ulong move) = player.DecideMove(board, 1);
            if (move != 0)
            {
                board = board.Reversed(move);
                return true;
            }
            return false;
        }

        public static void Test()
        {
            PlayerAI player = new PlayerAI(new EvaluatorPatternBased())
            {
                ParamBeg = new SearchParameters(depth: 11, stage: 0, new CutoffParameters(true, true, false)),
                ParamMid = new SearchParameters(depth: 11, stage: 16, new CutoffParameters(true, true, false)),
                ParamEnd = new SearchParameters(depth: 64, stage: 42, new CutoffParameters(true, true, false)),
                PrintInfo = false,
            };

            var solver = new MPCParamSolver(2, 9);

            for (int i = 0; i < 500; i++)
            {
                Board board = Tester.CreateRnadomGame(5);

                while (Step(solver, ref board, player, 1) | Step(solver, ref board, player, -1))
                {
                    GC.Collect();
                }

                solver.Export("data.dat");

                Console.WriteLine($"Game : {i}");
                Console.WriteLine($"B: {board.GetStoneCount(1)}");
                Console.WriteLine($"W: {board.GetStoneCount(-1)}");
                Console.WriteLine(i);
            }
        }

        public static Vector2 LinearRegression(IEnumerable<float> x, IEnumerable<float> y)
        {
            int n = x.Count();
            float ax = x.Average();
            float ay = y.Average();

            float a = x.Zip(y, (xi, yi) => (xi - ax) * (yi - ay)).Sum() / x.Select(x => (x - ax) * (x - ax)).Sum();

            return new Vector2(a, ay - a * ax);
        }

        public static Vector3 QuadraticRegression(IEnumerable<float> x, IEnumerable<float> y)
        {
            float x4 = x.Sum(t => t * t * t * t);
            float x3 = x.Sum(t => t * t * t);
            float x2 = x.Sum(t => t * t);
            float x1 = x.Sum();
            float n = x.Count();

            double[,] m = { { x4, x3, x2 }, { x3, x2, x1 }, { x2, x1, n } };

            float x2y1 = x.Zip(y).Sum(t => t.First * t.First * t.Second);
            float x1y1 = x.Zip(y).Sum(t => t.First * t.Second);
            float y1 = y.Sum();

            double[] v = { x2y1, x1y1, y1 };

            var result = Solver.GaussSeidel(m, v, 100, 0.001F);

            return new Vector3((float)result.Solution[0], (float)result.Solution[1], (float)result.Solution[2]);
        }

        int Depth1 { get; set; }
        int Depth2 { get; set; }

        public MPCParamSolver(int depthMin, int depthMax)
        {
            Depth1 = depthMin;
            Depth2 = depthMax;

            Data = new List<float>[Depth2 - Depth1 + 1][];
            for (int i = 0; i < Data.Length; i++)
            {
                Data[i] = new List<float>[60];

                for (int j = 0; j < Data[i].Length; j++)
                {
                    Data[i][j] = new List<float>();
                }
            }
        }

        List<float>[][] Data { get; }

        public void StackData(PlayerAI player, Board board)
        {
            foreach (Move m in new Move(board).NextMoves())
            {
                for (int i = Depth1; i <= Depth2; i++)
                {
                    float eval = player.Solve(new Search(), m, new CutoffParameters(true, true, false), i, -1000000, 1000000);
                    Data[i - Depth1][board.n_stone - 4].Add(eval);
                }
            }
        }

        public static float F(Vector3 a, float x) => a.X * x * x + a.Y * x + a.Z;

        public static (float a, float b, float sigma) CalcSigma(List<float> x, List<float> y)
        {
            Vector2 v = LinearRegression(x, y);
            float sigma = (float)Math.Sqrt(x.Zip(y, (x, y) => Math.Pow(y - v.X * x - v.Y, 2)).Sum() / y.Count);
            return (v.X, v.Y, sigma);
        }

        public (float a, float b, float sigma)[] CalcSigmaStageBased(int d1, int d2, int t1, int t2)
        {
            var result = new (float, float, float)[t2 - t1];
            var lists1 = Data[d1];
            var lists2 = Data[d2];

            for (int stage = t1; stage < t2; stage++)
            {
                var x = lists1[stage];
                var y = lists2[stage];

                result[stage - t1] = CalcSigma(x, y);
                Console.WriteLine($"{d1}, {d2}, {stage}: {result[stage - t1]}");
            }
            return result;
        }

        public Vector3[] CalcCoefficients(int d1, int d2, int t1, int t2)
        {
            IEnumerable<float> t = Enumerable.Range(0, t2 - t1).Select(i => (float)i);
            (float a, float b, float sigma)[] array = CalcSigmaStageBased(d1, d2, t1, t2);

            Vector3 coefficientA = QuadraticRegression(t, array.Select(v => v.a));
            Vector3 coefficientB = QuadraticRegression(t, array.Select(v => v.b));
            Vector3 coefficientSigma = QuadraticRegression(t, array.Select(v => v.sigma));

            Console.WriteLine($"Test: {d1}, {d2}, {F(coefficientSigma, 20)}");

            return new Vector3[] { coefficientA, coefficientB, coefficientSigma };
        }

        public class MPCParameters
        {
            Vector2 a, b, c;

            public MPCParameters(Vector2 a, Vector2 b, Vector2 c)
            {
                this.a = a;
                this.b = b;
                this.c = c;
            }

            public float F(Vector2 v, float x) => v.X * x + v.Y;
            public float Calc(float t, float d) => t * t * F(a, d) + t * F(b, d) + F(c, d);
        }

        public class MCP
        {
            private readonly MPCParameters a_param, b_param, sigma_param;
            private readonly int stage_start;

            public MCP(MPCParameters a_param, MPCParameters b_param, MPCParameters sigma_param, int stage_start)
            {
                this.a_param = a_param;
                this.b_param = b_param;
                this.sigma_param = sigma_param;
                this.stage_start = stage_start;
            }

            public (int lower, int upper) Test(int depth, int n_stone, float alpha, float beta)
            {
                n_stone -= stage_start + 4;
                depth -= 2;
                float a = a_param.Calc(depth, n_stone);
                float offset = b_param.Calc(depth, n_stone);
                float sigma = sigma_param.Calc(depth, n_stone);

                Console.WriteLine($"{a}, {offset}, {sigma}");

                float lower = (alpha - sigma - offset) / a;
                float upper = (beta + sigma - offset) / a;

                return ((int)lower, (int)upper);
            }
        }

        public MCP SolveParameters(int start, int count, int diff, int stageStart, int stageEnd)
        {
            MPCParameters Test(IEnumerable<Vector3> arr)
            {
                Vector2 te(Func<Vector3, float> map) => LinearRegression(Enumerable.Range(0, count).Select(i => (float)i), arr.Select(map));

                Vector2 a = te(v => v.X);
                Vector2 b = te(v => v.Y);
                Vector2 c = te(v => v.Z);

                return new MPCParameters(a, b, c);
            }

            Vector3[][] coefficients = new Vector3[count][];

            for (int i = 0; i < count; i++)
            {
                coefficients[i] = CalcCoefficients(start + i, start + i + diff, stageStart, stageEnd);
            }

            MPCParameters a = Test(coefficients.Select(v => v[0]));
            MPCParameters b = Test(coefficients.Select(v => v[1]));
            MPCParameters sigma = Test(coefficients.Select(v => v[2]));

            return new MCP(a, b, sigma, stageStart);
        }

        public static MPCParamSolver FromFile(string path)
        {
            using var reader = new BinaryReader(new FileStream(path, FileMode.Open));

            int d1 = reader.ReadInt32();
            int d2 = reader.ReadInt32();

            var solver = new MPCParamSolver(d1, d2);

            static float Clamp(float x, float range = 1000) => Math.Max(-range, Math.Min(range, x));

            for (int i = 0; i < solver.Data.Length; i++)
            {
                var d = solver.Data[i];

                for (int j = 0; j < d.Length; j++)
                {
                    int count = reader.ReadInt32();

                    for (int k = 0; k < count; k++)
                    {
                        d[j].Add(Clamp(reader.ReadSingle()));
                    }
                }
            }
            return solver;
        }

        public void Export(string path)
        {
            using var writer = new BinaryWriter(new FileStream(path, FileMode.Create));

            writer.Write(Depth1);
            writer.Write(Depth2);

            for (int i = 0; i < Data.Length; i++)
            {
                var d = Data[i];

                for (int j = 0; j < d.Length; j++)
                {
                    writer.Write(d[j].Count);
                    d[j].ForEach(writer.Write);
                }
            }
        }
    }
}
