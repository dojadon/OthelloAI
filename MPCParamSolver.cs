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
            PlayerAI player = new PlayerAI(new EvaluatorPatternBased_Release())
            {
                ParamBeg = new SearchParameters(depth: 11, stage: 0, new CutoffParameters(true, true, false)),
                ParamMid = new SearchParameters(depth: 11, stage: 16, new CutoffParameters(true, true, false)),
                ParamEnd = new SearchParameters(depth: 64, stage: 42, new CutoffParameters(true, true, false)),
                PrintInfo = false,
            };

            var solver = new MPCParamSolver(2, 9);

            for (int i = 0; i < 500; i++)
            {
                Board board = Tester.CreateRnadomGame(null, 5);

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

        public static float R2(IEnumerable<float> x, IEnumerable<float> y, Func<float, float> f)
        {
            float av = y.Average();
            return 1 - x.Zip(y).Sum(t => (t.Second - f(t.First)) * (t.Second - f(t.First))) / y.Sum(t => (t - av) * (t - av));
        }

        public static float R2(IEnumerable<int> x, IEnumerable<float> y, Func<float, float> f)
        {
            return R2(x.Select(v => (float)v), y, f);
        }

        public static Vector2 LinearRegression(IEnumerable<float> x, IEnumerable<float> y)
        {
            int n = x.Count();
            float ax = x.Average();
            float ay = y.Average();

            float a = x.Zip(y, (xi, yi) => (xi - ax) * (yi - ay)).Sum() / x.Select(x => (x - ax) * (x - ax)).Sum();

            return new Vector2(a, ay - a * ax);
        }

        public static Vector3 QuadraticRegression(IEnumerable<int> x, IEnumerable<float> y)
        {
            return QuadraticRegression(x.Select(v => (float)v), y);
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

        public static IEnumerable<float> Range(int start, int count) => Enumerable.Range(start, count).Select(i => (float)i);

        public static IEnumerable<float> Range(int start, int count, int step) => Enumerable.Range(0, count).Select(i => start + (float)i * step);

        public static float F(Vector3 a, float x) => a.X * x * x + a.Y * x + a.Z;

        public static float CalcSigma(float a, float b, List<float> x, List<float> y)
        {
            return (float)Math.Sqrt(x.Zip(y, (x, y) => Math.Pow(y - a * x - b, 2)).Sum() / y.Count);
        }

        public void CalcRegressionLineStageBased(int d1, int d2, int t1, int t2, out float[] a, out float[] b, out float[] sigma)
        {
            a = new float[t2 - t1];
            b = new float[t2 - t1];
            sigma = new float[t2 - t1];

            Console.WriteLine($"{d1}, {d2}");

            for (int stage = t1; stage < t2; stage++)
            {
                var xl = Data[d1][stage];
                var yl = Data[d2][stage];

                Vector2 v = LinearRegression(xl, yl);
                a[stage - t1] = v.X;
                b[stage - t1] = v.Y;

                sigma[stage - t1] = CalcSigma(v.X, v.Y, xl, yl);

                Console.WriteLine($"{a[stage - t1]}, {b[stage - t1]}, {sigma[stage - t1]}");
            }
        }

        public Vector2[] CalcRegressionLineStageBased(int d1, int d2, int t1, int t2)
        {
            var result = new Vector2[t2 - t1];

            for (int stage = t1; stage < t2; stage++)
            {
                result[stage - t1] = LinearRegression(Data[d1][stage], Data[d2][stage]);
            }
            return result;
        }

        public float[] CalcSigmaStageBased(IParameter ap, IParameter bp, int d1, int d2, int t1, int t2)
        {
            var result = new float[t2 - t1];

            for (int stage = t1; stage < t2; stage++)
            {
                var xl = Data[d1][stage];
                var yl = Data[d2][stage];

                float a = ap.Calc(stage - t1, d1);
                float b = bp.Calc(stage - t1, d1);

                result[stage - t1] = CalcSigma(a, b, xl, yl);
            }
            return result;
        }

        public (Vector3 a, Vector3 b) CalcCoefficients(int d1, int d2, int t1, int t2)
        {
            var t = Enumerable.Range(0, t2 - t1);
            Vector2[] a2 = CalcRegressionLineStageBased(d1, d2, t1, t2);

            Vector3 a = QuadraticRegression(t, a2.Select(v => v.X));
            Vector3 b = QuadraticRegression(t, a2.Select(v => v.Y));
            Console.WriteLine();
            Console.WriteLine($"[{d1}, {d2}]");
            Console.WriteLine($"B: {R2(t, a2.Select(v => v.Y), v => F(b, v))}");

            return (a, b);
        }

        public interface IParameter
        {
            public float Calc(int t, int d);
        }

        public class ParameterArray : IParameter
        {
            float[][] data;

            public ParameterArray(float[][] data)
            {
                this.data = data;
            }

            public float Calc(int t, int d)
            {
                return data[d][t];
            }
        }

        public class ParameterDoubleApproximation : IParameter
        {
            Vector2 a, b, c;

            public ParameterDoubleApproximation(Vector2 a, Vector2 b, Vector2 c)
            {
                this.a = a;
                this.b = b;
                this.c = c;
            }

            public float F(Vector2 v, float x) => v.X * x + v.Y;
            public float Calc(int t, int d) => t * t * F(a, d) + t * F(b, d) + F(c, d);
        }

        public class ParameterSkiped : IParameter
        {
            private readonly IParameter p1, p2;

            public ParameterSkiped(IParameter p1, IParameter p2)
            {
                this.p1 = p1;
                this.p2 = p2;
            }

            public float Calc(int t, int d) => (t + d) % 2 == 0 ? p1.Calc(t, d) : p2.Calc(t, d);
        }

        public class MCP
        {
            private readonly IParameter a_param, b_param, sigma_param;
            private readonly int stage_offset, depth_offset;

            public MCP(IParameter a_param, IParameter b_param, IParameter sigma_param, int depth_offset, int stage_offset)
            {
                this.a_param = a_param;
                this.b_param = b_param;
                this.sigma_param = sigma_param;
                this.depth_offset = depth_offset;
                this.stage_offset = stage_offset;
            }

            public (int lower, int upper) Test(int depth, int n_stone, float alpha, float beta, float t)
            {
                n_stone -= stage_offset + 4;
                depth -= depth_offset;
                float a = a_param.Calc(n_stone, depth);
                float offset = b_param.Calc(n_stone, depth);
                float sigma = sigma_param.Calc(n_stone, depth) * t;

                //Console.WriteLine($"{depth}, {n_stone}: {a}, {offset}, {sigma}");

                float lower = (alpha - sigma - offset) / a;
                float upper = (beta + sigma - offset) / a;

                return ((int)lower, (int)upper);
            }
        }

        public MCP SolveParameters(int diff, int stageStart, int stageEnd)
        {
            int count = Data.Length - diff;

            IParameter Test(IEnumerable<Vector3> arr)
            {
                Vector2 te(Func<Vector3, float> map) => LinearRegression(Range(0, count), arr.Select(map));

                Vector2 a = te(v => v.X);
                Vector2 b = te(v => v.Y);
                Vector2 c = te(v => v.Z);

                return new ParameterDoubleApproximation(a, b, c);
            }

            (Vector3 a, Vector3 b)[] coefficients = new (Vector3, Vector3)[count];

            for (int i = 0; i < count; i++)
            {
                coefficients[i] = CalcCoefficients(i, i + diff, stageStart, stageEnd);
            }

            IParameter a = Test(coefficients.Select(v => v.a));
            IParameter b = Test(coefficients.Select(v => v.b));

            Vector3[] sigmaCoefficients = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                var c = CalcSigmaStageBased(a, b, i, i + diff, stageStart, stageEnd);
                sigmaCoefficients[i] = QuadraticRegression(Range(0, stageEnd - stageStart), c);
            }

            IParameter sigma = Test(sigmaCoefficients);

            return new MCP(a, b, sigma, Depth1, stageStart);
        }

        public MCP SolveParameters2(int diff, int stageStart, int stageEnd)
        {
            int count = Data.Length - diff;

            float[][] a_a = new float[count][];
            float[][] b_a = new float[count][];
            float[][] s_a = new float[count][];

            for (int i = 0; i < count; i++)
            {
                CalcRegressionLineStageBased(i, i + diff, stageStart, stageEnd, out a_a[i], out b_a[i], out s_a[i]);
            }

            IParameter a = new ParameterArray(a_a);
            IParameter b = new ParameterArray(b_a);
            IParameter sigma = new ParameterArray(s_a);

            return new MCP(a, b, sigma, Depth1, stageStart);
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
