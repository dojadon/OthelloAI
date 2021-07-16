using System;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.X86;

namespace OthelloAI.Patterns
{
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
            Rotated270 = Transposed.VerticalMirrored();
            Rotated90 = Transposed.HorizontalMirrored();
        }

        public static void Create(Board org, out Board tr, out Board hor, out Board rot90, out Board rot270)
        {
            tr = org.Transposed();
            hor = org.HorizontalMirrored();
            rot90 = tr.VerticalMirrored();
            rot270 = tr.HorizontalMirrored();
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

                for(int i = 0; i < 10; i++)
                {
                    result += ((b >> i) & 1) * POW3_TABLE[i];
                }
                return result;
            }

            for(int i = 0; i < TERNARY_TABLE.Length; i++)
            {
                TERNARY_TABLE[i] = Convert(i) + Convert(i >> 10) * 2;
            }
        }

        public static readonly int[] POW3_TABLE = { 1, 3, 9, 27, 81, 243, 729, 2187, 6561, 19683, 59049 };

        public const int STAGES = 60;

        protected string FilePath { get; }

        public abstract int HashLength { get; }
        public int ArrayLength => POW3_TABLE[HashLength];

        public abstract ulong Mask { get; }

        protected float[][] StageBasedEvaluations { get; } = new float[STAGES][];

        public Pattern(string filePath)
        {
            FilePath = filePath;
        }

        public virtual void Init()
        {
            for (int i = 0; i < STAGES; i++)
            {
                StageBasedEvaluations[i] = new float[GetArrayLength()];
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

        public int GetArrayLength()
        {
            return POW3_TABLE[HashLength];
        }

        public int GetBinHash(Board board, ulong mask)
        {
            return (int)(Bmi2.X64.ParallelBitExtract(board.bitB, mask) | (Bmi2.X64.ParallelBitExtract(board.bitW, mask) << 10));
        }

        public int GetHash(Board board) 
        {
            return TERNARY_TABLE[GetBinHash(board, Mask)];
        }

        public virtual float Eval(Board org, Board tr, Board hor, Board rot90, Board rot270, int stone)
        {
            float[] eval = StageBasedEvaluations[GetStage(org)];

            float result = eval[GetHash(org)];
            result += eval[GetHash(tr)];
            result += eval[GetHash(hor)];
            result += eval[GetHash(rot90)];
            result += eval[GetHash(rot270)];

            return result * stone;
        }

        public int ConvertHash(int hash)
        {
            int result = 0;
            for (int i = 0; i < HashLength; i++)
            {
                int id = hash % 3;
                switch (id)
                {
                    case 1:
                        result |= 1 << i;
                        break;

                    case 2:
                        result |= 1 << (i + HashLength);
                        break;
                }
                hash /= 3;
            }
            return result;
        }

        public Board SetBoard(int hash)
        {
            ulong b = 0;
            ulong w = 0;
            ulong mask = Mask;

            for(int i = 0; i < 64; i++)
            {
                if(((mask >> i) & 1) != 0)
                {
                    int id = hash % 3;
                    switch (id)
                    {
                        case 1:
                            b |= Board.Mask(i);
                            break;

                        case 2:
                            w |= Board.Mask(i);
                            break;
                    }
                    hash /= 3;
                }
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

        public abstract int GetHashMirrored(int hash);

        public static int Reverse(int value) => (int)Reverse((uint)value);

        public static uint Reverse(uint value)
        {
            return (value & 0xFF) << 24 |
                    ((value >> 8) & 0xFF) << 16 |
                    ((value >> 16) & 0xFF) << 8 |
                    ((value >> 24) & 0xFF);
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
            return Enumerable.Range(0, GetArrayLength()).All(i => GetHash(SetBoard(i)) == i);
        }

        public void Info(int stage, float threshold)
        {
            for (int i = 0; i < GetArrayLength(); i++)
            {
                if (StageBasedEvaluations[stage][i]  > threshold)
                {
                    InfoHash(stage, i);
                }
            }
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
