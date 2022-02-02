using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.X86;

namespace OthelloAI.Patterns
{
    public enum PatternType
    {
        X_SYMETRIC,
        XY_SYMETRIC,
        DIAGONAL,
    }

    public class PatternEdge2X : Pattern
    {
        public override int[] Positions { get; } = { 0, 1, 2, 3, 4, 5, 6, 7, 9, 14 };

        public PatternEdge2X(string filePath, PatternType type) : base(filePath, type, 10)
        {
        }

        public override int Hash(in Board board, int index)
        {
            return (int)((board.bitB & 0xFF) | ((board.bitB & 0x200) >> 1) | ((board.bitB & 0x4000) >> 5) | ((board.bitW & 0xFF) << 10) | ((board.bitW & 0x200) << 9) | ((board.bitW & 0x4000) << 5));
        }
    }

    public class PatternVerticalLine3 : Pattern
    {
        public override int[] Positions { get; } = { 24, 25, 26, 27, 28, 29, 30, 31 };

        public PatternVerticalLine3(string filePath, PatternType type) : base(filePath, type, 8)
        {
        }

        public override int Hash(in Board board, int index)
        {
            return index switch
            {
                0 => (int)(((board.bitB & 0xFF000000) >> 24) | ((board.bitW & 0xFF000000) >> 16)),
                1 => (int)(((board.bitB & 0x000000FF) >> 24) | ((board.bitW & 0x000000FF) >> 16)),
                _ => throw new NotImplementedException($"index : {index}"),
            };
        }
    }

    public class PatternVerticalLine2 : Pattern
    {
        public override int[] Positions { get; } = { 16, 17, 18, 19, 20, 21, 22, 23 };

        public PatternVerticalLine2(string filePath, PatternType type) : base(filePath, type, 8)
        {
        }

        public override int Hash(in Board board, int index)
        {
            return (int)(((board.bitB & 0xFF0000) >> 16) | ((board.bitW & 0xFF0000) >> 8));
        }
    }

    public class PatternVerticalLine1 : Pattern
    {
        public override int[] Positions { get; } = { 8, 9, 10, 11, 12, 13, 14, 15 };

        private const ulong MASK1 = 0xFF00;
        private const ulong MASK2 = 0x00FF_0000_0000_0000;

        public PatternVerticalLine1(string filePath, PatternType type) : base(filePath, type, 8)
        {
        }

        public override int Hash(in Board board, int index)
        {
            return (int)(index switch
            {
                0 => (board.bitB & MASK1) >> 8 | (board.bitW & MASK1),
                1 => (board.bitB & MASK2) >> 48 | (board.bitW & MASK2) >> 40,
                _ => throw new NotImplementedException($"index : {index}"),
            });
        }
    }

    public enum IndexingType
    {
        BIN, TER
    }

    public abstract class Pattern
    {
        public const int STAGES = 60;

        protected string FilePath { get; }

        protected PatternType Type { get; }
        public IBoardIndexer BoardIndexer { get; }

        public int HashLength { get; }
        public int ArrayLength => BinTerUtil.POW3_TABLE[HashLength];
        public virtual int NumPatterns => 4;

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
            int length = BoardIndexer.IndexingType switch
            {
                IndexingType.BIN => 1 << (2 * HashLength),
                IndexingType.TER => BinTerUtil.POW3_TABLE[HashLength],
                _ => throw new NotImplementedException()
            };

            for (int i = 0; i < STAGES; i++)
            {
                StageBasedEvaluations[i] = new float[length];
                StageBasedEvaluationsB[i] = new byte[length];
            }
        }

        protected int GetStage(Board board)
        {
            return board.n_stone - 5;
        }

        public void UpdataEvaluation(Board board, float add)
        {
            int stage = GetStage(board);

            int hash = Hash(board, 0);
            int flipped = FlipStone(hash);

            StageBasedEvaluations[stage][hash] += add;
            StageBasedEvaluations[stage][flipped] -= add;
        }



        public int Hash(in Board board, int index) => BoardIndexer.IndexingType switch
        {
            IndexingType.BIN => HashBin(board),
            IndexingType.TER => HashTer(board, index),
            _ => throw new NotImplementedException()
        };

        public abstract int HashBin(in Board board);

        public abstract int HashTer(in Board board, int index);

        public byte Eval(in Board b, byte[] a, int index) => a[BoardIndexer.Hash(b)];

        public float Eval(in Board b, float[] a, int index) => a[BoardIndexer.Hash(b)];

        public float EvalForTraining(in Board b)
        {
            float[] e = StageBasedEvaluations[GetStage(b)];
            return Eval(b, e, 0) + Eval(b, e, 1) + Eval(b, e, 2) + Eval(b, e, 3);
        }

        public int Eval(in Board b)
        {
            byte[] e = StageBasedEvaluationsB[GetStage(b)];
            return Eval(b, e, 0) + Eval(b, e, 1) + Eval(b, e, 2) + Eval(b, e, 3);
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
                        b |= Board.Mask(BoardIndexer.Positions[i]);
                        break;

                    case 2:
                        w |= Board.Mask(BoardIndexer.Positions[i]);
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
                result += s * BinTerUtil.POW3_TABLE[i];
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
            using var reader_sub = new BinaryReader(new FileStream("eval_old\\" + FilePath, FileMode.Open));

            static byte ConvertToInt8(float e)
            {
                return (byte)Math.Clamp(e * 32 + 128, 0, 255);
            }

            for (int stage = 0; stage < STAGES; stage++)
            {
                for (int i = 0; i < ArrayLength; i++)
                {
                    float e = reader.ReadSingle();

                    //if(e == 0)
                    {
                        int win = BinaryPrimitives.ReverseEndianness(reader_sub.ReadInt32());
                        int game = BinaryPrimitives.ReverseEndianness(reader_sub.ReadInt32());

                        if (game != 0)
                        {
                            e = (float)win / game;
                            //Console.WriteLine(e);
                        }
                    }

                    int index;
                    switch (BoardIndexer.IndexingType)
                    {
                        case IndexingType.BIN:
                            (int b1, int b2) = BinTerUtil.ConvertTerToBinPair(i, HashLength);
                            index = b1 | (b2 << HashLength);
                            break;

                        case IndexingType.TER:
                            index = i;
                            break;

                        default:
                            throw new NotImplementedException();
                    }

                    StageBasedEvaluations[stage][index] = e;
                    StageBasedEvaluationsB[stage][index] = ConvertToInt8(e);

                    //int count = 2;
                    //if (stage >= count)
                    //    e = Enumerable.Range(0, count).Select(k => StageBasedEvaluations[stage - k][i]).Average();
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
            return Enumerable.Range(0, ArrayLength).All(i => Hash(SetBoard(i), 0) == i);
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
            Console.WriteLine(SetBoard(hash));
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
