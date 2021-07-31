using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.X86;

namespace OthelloAI.Patterns
{
    public enum PatternType
    {
        X_SYMETRIC, XY_SYMETRIC, DIAGONAL
    }

    public class MirroredNeededBoards
    {
        public Board Original { get; }
        public Board Transposed { get; }
        public Board HorizontalMirrored { get; }
        public Board Rotated90 { get; }
        public Board Rotated270 { get; }

        public MirroredNeededBoards(Board source)
        {
            Original = source;
            Transposed = source.Transposed();
            HorizontalMirrored = source.HorizontalMirrored();
            Rotated270 = HorizontalMirrored.Transposed();
            Rotated90 = Transposed.HorizontalMirrored();
        }

        public static void Create(Board org, out Board tr, out Board hor, out Board rot90, out Board rot270)
        {
            tr = org.Transposed();
            hor = org.HorizontalMirrored();
            rot90 = hor.Transposed();
            rot270 = tr.HorizontalMirrored();
        }
    }

    public class PatternEdge2X : Pattern
    {
        public override int[] Positions { get; } = { 0, 1, 2, 3, 4, 5, 6, 7, 9, 14 };

        public PatternEdge2X(string filePath, PatternType type) : base(filePath, type, 10)
        {
        }

        public override int GetBinHash(in Board board)
        {
            return (int)((board.bitB & 0xFF) | ((board.bitB & 0x200) >> 1) | ((board.bitB & 0x4000) >> 5) | ((board.bitW & 0xFF) << 10) | ((board.bitW & 0x200) << 9) | ((board.bitW & 0x4000) << 5));
        }

        public override int GetBinHash1(in Board board)
        {
            return (int)((board.bitB & 0xFF) | ((board.bitB & 0x200) >> 1) | ((board.bitB & 0x4000) >> 5));
        }

        public override int GetBinHash2(in Board board)
        {
            return (int)((board.bitW & 0xFF) | ((board.bitW & 0x200) >> 1) | ((board.bitW & 0x4000) >> 5));
        }
    }

    public class PatternVerticalLine3 : Pattern
    {
        public override int[] Positions { get; } = { 24, 25, 26, 27, 28, 29, 30, 31 };

        public PatternVerticalLine3(string filePath, PatternType type) : base(filePath, type, 8)
        {
        }

        public override int GetBinHash(in Board board)
        {
            return (int)(((board.bitB & 0xFF000000) >> 24) | ((board.bitW & 0xFF000000) >> 16));
        }

        public override int GetBinHash1(in Board board)
        {
            return (int)((board.bitB & 0xFF00) >> 24);
        }

        public override int GetBinHash2(in Board board)
        {
            return (int)((board.bitW & 0xFF00) >> 24);
        }
    }

    public class PatternVerticalLine2 : Pattern
    {
        public override int[] Positions { get; } = { 16, 17, 18, 19, 20, 21, 22, 23 };

        public PatternVerticalLine2(string filePath, PatternType type) : base(filePath, type, 8)
        {
        }

        public override int GetBinHash(in Board board)
        {
            return (int)(((board.bitB & 0xFF0000) >> 16) | ((board.bitW & 0xFF0000) >> 8));
        }

        public override int GetBinHash1(in Board board)
        {
            return (int)((board.bitB & 0xFF00) >> 16);
        }

        public override int GetBinHash2(in Board board)
        {
            return (int)((board.bitW & 0xFF00) >> 16);
        }
    }

    public class PatternVerticalLine1 : Pattern
    {
        public override int[] Positions { get; } = { 8, 9, 10, 11, 12, 13, 14, 15 };

        public PatternVerticalLine1(string filePath, PatternType type) : base(filePath, type, 8)
        {
        }

        public override int GetBinHash(in Board board)
        {
            return (int)(((board.bitB & 0xFF00) >> 8) | (board.bitW & 0xFF00));
        }

        public override int GetBinHash1(in Board board)
        {
            return (int)((board.bitB & 0xFF00) >> 8);
        }

        public override int GetBinHash2(in Board board)
        {
            return (int)((board.bitW & 0xFF00) >> 8);
        }
    }

    public class PatternVerticalLine0 : Pattern
    {
        public override int[] Positions { get; } = { 0, 1, 2, 3, 4, 5, 6, 7 };

        public PatternVerticalLine0(string filePath, PatternType type) : base(filePath, type, 8)
        {
        }

        public override int GetBinHash(in Board board)
        {
            return (int)((board.bitB & 0xFF) | ((board.bitW & 0xFF) << 8));
        }

        public override int GetBinHash1(in Board board)
        {
            return (int)(board.bitB & 0xFF);
        }

        public override int GetBinHash2(in Board board)
        {
            return (int)(board.bitW & 0xFF);
        }
    }

    public class PatternBitMask : Pattern
    {
        public ulong Mask { get; }

        public override int[] Positions { get; }

        public PatternBitMask(string filePath, PatternType type, int length, ulong mask) : base(filePath, type, length)
        {
            Mask = mask;
            Positions = new int[length];

            int index = 0;
            for (int i = 0; i < 64; i++)
            {
                if (((mask >> i) & 1) != 0)
                {
                    Positions[index++] = i;
                }
            }
        }

        public override int GetBinHash(in Board board)
        {
            return (int)(Bmi2.X64.ParallelBitExtract(board.bitB, Mask) | (Bmi2.X64.ParallelBitExtract(board.bitW, Mask) << HashLength));
        }

        public override int GetBinHash1(in Board board)
        {
            return (int)Bmi2.X64.ParallelBitExtract(board.bitB, Mask);
        }

        public override int GetBinHash2(in Board board)
        {
            return (int)Bmi2.X64.ParallelBitExtract(board.bitW, Mask);
        }
    }

    public abstract class Pattern
    {
        public static readonly int[] POW3_TABLE = { 1, 3, 9, 27, 81, 243, 729, 2187, 6561, 19683, 59049 };

        public static readonly int[][] TERNARY_TABLES = Enumerable.Range(1, 10).Select(CreateTernaryTableHalf).ToArray();

        public static (int, int) ConvertTerToBinPair(int value, int length)
        {
            int b1= 0;
            int b2= 0;
            for (int i = 0; i < length; i++)
            {
                switch (value % 3)
                {
                    case 1:
                        b1 |= 1 << i;
                        break;

                    case 2:
                        b2 |= 1 << i;
                        break;
                }
                value /= 3;
            }
            return (b1, b2);
        }

        public static int ConvertBinToTer(int value, int length)
        {
            int result = 0;

            for (int i = 0; i < length; i++)
            {
                result += ((value >> i) & 1) * POW3_TABLE[i];
            }
            return result;
        }

        public static int[] CreateTernaryTable(int length)
        {
            int Convert(int b)
            {
                int result = 0;

                for (int i = 0; i < length; i++)
                {
                    result += ((b >> i) & 1) * POW3_TABLE[i];
                }
                return result;
            }

            int[] result = new int[1 << (length * 2)];

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = Convert(i) + Convert(i >> length) * 2;
            }
            return result;
        }

        public static int[] CreateTernaryTableHalf(int length)
        {
            int Convert(int b)
            {
                int result = 0;

                for (int i = 0; i < length; i++)
                {
                    result += ((b >> i) & 1) * POW3_TABLE[i];
                }
                return result;
            }

            int[] result = new int[1 << length];

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = Convert(i);
            }
            return result;
        }

        public const int STAGES = 60;

        protected string FilePath { get; }

        protected PatternType Type { get; }

        public int HashLength { get; }
        public int ArrayLength => POW3_TABLE[HashLength];

        int[] TernaryTable { get; }

        public abstract int[] Positions { get; }

        protected float[][] StageBasedEvaluations { get; } = new float[STAGES][];

        protected byte[][] StageBasedEvaluationsB { get; } = new byte[STAGES][];

        protected byte[][] EvaluationsBin { get; } = new byte[STAGES][];

        public Pattern(string filePath, PatternType type, int length)
        {
            FilePath = filePath;
            Type = type;
            HashLength = length;
            TernaryTable = TERNARY_TABLES[length - 1];
        }

        public virtual void Init()
        {
            for (int i = 0; i < STAGES; i++)
            {
                StageBasedEvaluations[i] = new float[ArrayLength];
                StageBasedEvaluationsB[i] = new byte[ArrayLength];
                EvaluationsBin[i] = new byte[1 << (2 * HashLength)];
            }
        }

        protected int GetStage(Board board)
        {
            return board.n_stone - 5;
        }

        public void UpdataEvaluation(Board board, float add)
        {
            int stage = GetStage(board);

            int hash = GetHash(board);
            int flipped = FlipStone(hash);

            StageBasedEvaluations[stage][hash] += add;
            StageBasedEvaluations[stage][flipped] -= add;
        }

        public abstract int GetBinHash(in Board board);
        public abstract int GetBinHash1(in Board board);
        public abstract int GetBinHash2(in Board board);

        public int GetHash(in Board board)
        {
            return GetBinHash(board);

            int hash1 = GetBinHash1(board);
            int hash2 = GetBinHash2(board);

            if (hash1 == 0 && hash2 == 0)
                return 0;
            /*else if (hash1 == 0)
                return TernaryTableHalf[hash2] * 2;
            else if (hash2 == 0)
                return TernaryTableHalf[hash1];*/
            else
                return TernaryTable[hash1] + TernaryTable[hash2] * 2;
        }

        public float EvalForTraining(in Board org, in Board tr, in Board hor, in Board rot90, in Board rot270)
        {
            float[] eval = StageBasedEvaluations[GetStage(org)];

            float result = Type switch
            {
                PatternType.X_SYMETRIC => eval[GetHash(org)] + eval[GetHash(tr)] + eval[GetHash(hor)] + eval[GetHash(rot90)],
                PatternType.XY_SYMETRIC => eval[GetHash(org)] + eval[GetHash(tr)] + eval[GetHash(hor)] + eval[GetHash(rot270)],
                PatternType.DIAGONAL => eval[GetHash(org)] + eval[GetHash(hor)],
                _ => throw new NotImplementedException()
            };
            return result;
        }

        public float Eval(in Board org, in Board tr, in Board hor, in Board rot90, in Board rot270)
        {
           // byte[] eval = StageBasedEvaluationsB[GetStage(org)];
            byte[] eval = EvaluationsBin[GetStage(org)];

            return Type switch
            {
                PatternType.X_SYMETRIC => eval[GetHash(org)] + eval[GetHash(tr)] + eval[GetHash(hor)] + eval[GetHash(rot90)] - 512,
                PatternType.XY_SYMETRIC => eval[GetHash(org)] + eval[GetHash(tr)] + eval[GetHash(hor)] + eval[GetHash(rot270)] - 512,
                PatternType.DIAGONAL => eval[GetHash(org)] + eval[GetHash(hor)] - 256,
                _ => throw new NotImplementedException()
            };
        }

        public Board SetBoard(int hash)
        {
            ulong b = 0;
            ulong w = 0;

            for (int i = 0; i < HashLength; i++)
            {
                int id = hash % 3;
                switch (id)
                {
                    case 1:
                        b |= Board.Mask(Positions[i]);
                        break;

                    case 2:
                        w |= Board.Mask(Positions[i]);
                        break;
                }
                hash /= 3;
            }
            return new Board(b, w);
        }

        public int FlipStone(int hash)
        {
            int result = 0;

            for (int i = 0; i < HashLength; i++)
            {
                int s = hash % 3;
                hash /= 3;
                s = s == 0 ? 0 : (s == 1 ? 2 : 1);
                result += s * POW3_TABLE[i];
            }
            return result;
        }

        public void Load()
        {
            if (!File.Exists(FilePath))
            {
                return;
            }

            using var reader = new BinaryReader(new FileStream(FilePath, FileMode.Open));

            for (int stage = 0; stage < STAGES; stage++)
            {
                for (int i = 0; i < ArrayLength; i++)
                {
                    float e = reader.ReadSingle();

                    StageBasedEvaluations[stage][i] = e;
                    StageBasedEvaluationsB[stage][i] = (byte)Math.Clamp(e * 32 + 128, byte.MinValue, byte.MaxValue);

                    (int b1, int b2) = ConvertTerToBinPair(i, HashLength);
                    int hash = b1 | (b2 << HashLength);
                    EvaluationsBin[stage][hash] = StageBasedEvaluationsB[stage][i];
                }
            }
        }

        public void Save()
        {
            using var writer = new BinaryWriter(new FileStream(FilePath, FileMode.Create));

            for (int stage = 0; stage < STAGES; stage++)
            {
                for (int i = 0; i < ArrayLength; i++)
                {
                    writer.Write(StageBasedEvaluations[stage][i]);
                }
            }
        }

        public bool Test()
        {
            return Enumerable.Range(0, ArrayLength).All(i => GetHash(SetBoard(i)) == i);
        }

        public void PrintArray(int start, int end)
        {
            foreach ((int i, double e) in Enumerable.Range(0, ArrayLength).Select(i => (i, Enumerable.Range(start, end - start).Average(j => StageBasedEvaluationsB[j][i]))))
            {
                if (Math.Abs(e - 128) > 5)
                    Console.Write("{" + i + ", " + (byte)e + "},");
            }

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
        }

        public void Info(int stage, int threshold)
        {
            for (int i = 0; i < StageBasedEvaluations[stage].Length; i++)
            {
                if (StageBasedEvaluations[stage][i] > threshold)
                {
                    InfoHash(stage, i);
                }
            }
            // Console.WriteLine(Enumerable.Range(0, ArrayLength).Count(i => StageBasedEvaluationsB[stage][i] > threshold));
        }

        public void InfoHash(int stage, int hash)
        {
            SetBoard(hash).print();
            Console.WriteLine($"Stage : {stage}, Hash : {hash}");
            Console.WriteLine($"Eval : {StageBasedEvaluations[stage][hash]}");
            Console.WriteLine();
        }

        public float Entropy(float p)
        {
            if (0 < p && p < 1)
            {
                return (float)-((p * Math.Log(p) + (1 - p) * Math.Log(1 - p)) / Math.Log(2));
            }
            return 0;
        }
    }
}
