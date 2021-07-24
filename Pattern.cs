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
        public override int[] Positions { get; } = { 0, 1, 2, 3, 4, 5, 6, 7, 9, 14};

        public PatternEdge2X(string filePath, PatternType type) : base(filePath, type, 10)
        {
        }

        public override int GetBinHash(in Board board)
        {
            return (int)((board.bitB & 0xFF) | ((board.bitB & 0x200) >> 1) | ((board.bitB & 0x4000) >> 5) | ((board.bitW & 0xFF) << 10) | ((board.bitW & 0x200) << 9) | ((board.bitW & 0x4000) << 5));
        }
    }

    public class PatternVerticalLine3 : Pattern
    {
        public override int[] Positions { get; } = { 24, 25, 26, 27, 28, 29, 30, 31};

        public PatternVerticalLine3(string filePath, PatternType type) : base(filePath, type, 8)
        {
        }

        public override int GetBinHash(in Board board)
        {
            return (int)(((board.bitB & 0xFF000000) >> 24) | ((board.bitW & 0xFF000000) >> 14));
        }
    }

    public class PatternVerticalLine2 : Pattern
    {
        public override int[] Positions { get; } = { 16, 17, 18, 19, 20, 21, 22, 23};

        public PatternVerticalLine2(string filePath, PatternType type) : base(filePath, type, 8)
        {
        }

        public override int GetBinHash(in Board board)
        {
            return (int)(((board.bitB & 0xFF0000) >> 16) | ((board.bitW & 0xFF0000) >> 6));
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
            return (int)((board.bitB & 0xFF) | ((board.bitW & 0xFF) << 10));
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
            return (int)(((board.bitB & 0xFF00) >> 8) | ((board.bitW & 0xFF00) << 2));
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
            return (int)(Bmi2.X64.ParallelBitExtract(board.bitB, Mask) | (Bmi2.X64.ParallelBitExtract(board.bitW, Mask) << 10));
        }
    }

    public abstract class Pattern
    {
        public static readonly int[] TERNARY_TABLE = new int[1 << 20];

        public static void InitTable()
        {
            static int Convert(int b)
            {
                int result = 0;

                for (int i = 0; i < 10; i++)
                {
                    result += ((b >> i) & 1) * POW3_TABLE[i];
                }
                return result;
            }

            for (int i = 0; i < TERNARY_TABLE.Length; i++)
            {
                TERNARY_TABLE[i] = Convert(i) + Convert(i >> 10) * 2;
            }
        }

        public static readonly int[] POW3_TABLE = { 1, 3, 9, 27, 81, 243, 729, 2187, 6561, 19683, 59049 };

        public const int STAGES = 60;

        protected string FilePath { get; }

        protected PatternType Type { get; }

        public int HashLength { get; }
        public int ArrayLength => POW3_TABLE[HashLength];

        public abstract int[] Positions { get; }

        protected float[][] StageBasedEvaluations { get; } = new float[STAGES][];

        protected byte[][] StageBasedEvaluationsB { get; } = new byte[STAGES][];

        public Pattern(string filePath, PatternType type, int length)
        {
            FilePath = filePath;
            Type = type;
            HashLength = length;
        }

        public virtual void Init()
        {
            for (int i = 0; i < STAGES; i++)
            {
                StageBasedEvaluations[i] = new float[ArrayLength];
                StageBasedEvaluationsB[i] = new byte[ArrayLength];
            }
        }

        protected int GetStage(Board board)
        {
            return board.stoneCount - 5;
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

        public int GetHash(in Board board)
        {
            return TERNARY_TABLE[GetBinHash(board)];
        }

        public float EvalForTraining(in Board org, in Board tr, in Board hor, in Board rot90, in Board rot270)
        {
            float[] eval = StageBasedEvaluations[GetStage(org)];

            int h1 = GetHash(org);
            int h2 = GetHash(tr);
            int h3 = GetHash(hor);
            int h4 = GetHash(rot90);
            int h5= GetHash(rot270);

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
            byte[] eval = StageBasedEvaluationsB[GetStage(org)];

            return Type switch
            {
                PatternType.X_SYMETRIC => eval[GetHash(org)] + eval[GetHash(tr)] + eval[GetHash(hor)] + eval[GetHash(rot90)],
                PatternType.XY_SYMETRIC => eval[GetHash(org)] + eval[GetHash(tr)] + eval[GetHash(hor)] + eval[GetHash(rot270)],
                PatternType.DIAGONAL => eval[GetHash(org)] + eval[GetHash(hor)],
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
                    StageBasedEvaluations[stage][i] = reader.ReadSingle();
                    StageBasedEvaluationsB[stage][i] = (byte)Math.Clamp(StageBasedEvaluations[stage][i] * 32 + 128, byte.MinValue, byte.MaxValue);
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
            foreach((int i, double e) in Enumerable.Range(0, ArrayLength).Select(i => (i, Enumerable.Range(start, end - start).Average(j => StageBasedEvaluationsB[j][i]))))
            {
                if(Math.Abs(e - 128) > 5)
                    Console.Write("{" + i + ", " + (byte)e + "},");
            }

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
        }

        public void Info(int stage, int threshold)
        {
            for (int i = 0; i < ArrayLength; i++)
            {
                if (StageBasedEvaluationsB[stage][i] > threshold)
                {
                    //InfoHash(stage, i);
                }
            }
            Console.WriteLine(Enumerable.Range(0, ArrayLength).Count(i => StageBasedEvaluationsB[stage][i] > threshold));
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
