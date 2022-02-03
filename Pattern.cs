using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;

namespace OthelloAI.Patterns
{
    public enum PatternType
    {
        X_SYMETRIC,
        XY_SYMETRIC,
        DIAGONAL,
    }

    public enum NAry
    {
        BIN, TER
    }

    public class Boards
    {
        public Board Original { get; }
        public Board Transposed { get; }
        public Board HorizontalMirrored { get; }
        public Board Rotated90 { get; }
        public Board Rotated270 { get; }

        public Boards(Board source)
        {
            Original = source;
            Transposed = source.Transposed();
            HorizontalMirrored = source.HorizontalMirrored();
            Rotated270 = HorizontalMirrored.Transposed();
            Rotated90 = Transposed.HorizontalMirrored();
        }
    }

    public class Pattern
    {
        public const int STAGES = 60;

        protected string FilePath { get; }

        protected PatternType Type { get; }
        protected NAry NAry { get; }
        public BoardHasher Hasher { get; }

        public int ArrayLength { get; }
        public int NumOfStates { get; }

        protected float[][] StageBasedEvaluations { get; } = new float[STAGES][];
        protected byte[][] StageBasedEvaluationsB { get; } = new byte[STAGES][];

        public Pattern(string filePath, BoardHasher hasher, PatternType type, NAry nary)
        {
            FilePath = filePath;
            Hasher = hasher;
            Type = type;
            NAry = nary;

            NumOfStates = (int)Math.Pow(3, Hasher.HashLength);

            ArrayLength = NAry switch
            {
                NAry.BIN => (int)Math.Pow(2, 2 * Hasher.HashLength),
                NAry.TER => NumOfStates,
                _ => throw new NotImplementedException()
            };

            for (int i = 0; i < STAGES; i++)
            {
                StageBasedEvaluations[i] = new float[ArrayLength];
                StageBasedEvaluationsB[i] = new byte[ArrayLength];
            }
        }

        protected int GetStage(Board board)
        {
            return board.n_stone - 5;
        }

        public void UpdataEvaluation(Boards boards, float add)
        {
            switch (Type)
            {
                case PatternType.X_SYMETRIC:
                    UpdataEvaluation(boards.Original, add);
                    UpdataEvaluation(boards.HorizontalMirrored, add);
                    UpdataEvaluation(boards.Transposed, add);
                    UpdataEvaluation(boards.Rotated90, add);
                    break;

                case PatternType.XY_SYMETRIC:
                    UpdataEvaluation(boards.Original, add);
                    UpdataEvaluation(boards.HorizontalMirrored, add);
                    UpdataEvaluation(boards.Transposed, add);
                    UpdataEvaluation(boards.Rotated270, add);
                    break;

                case PatternType.DIAGONAL:
                    UpdataEvaluation(boards.Original, add);
                    UpdataEvaluation(boards.HorizontalMirrored, add);
                    break;
            }
        }

        public void UpdataEvaluation(Board board, float add)
        {
            int stage = GetStage(board);

            int hash = Hasher.HashByPEXT(board, NAry);
            int flipped = FlipStone(hash);

            StageBasedEvaluations[stage][hash] += add;
            StageBasedEvaluations[stage][flipped] -= add;
        }

        public int EvalByPEXTHashing(Boards b)
        {
            byte[] e = StageBasedEvaluationsB[GetStage(b.Original)];
            byte _Eval(in Board borad) => e[Hasher.HashByPEXT(borad, NAry)];

            return Type switch
            {
                PatternType.X_SYMETRIC => _Eval(b.Original) + _Eval(b.HorizontalMirrored) + _Eval(b.Transposed) + _Eval(b.Rotated90),
                PatternType.XY_SYMETRIC => _Eval(b.Original) + _Eval(b.HorizontalMirrored) + _Eval(b.Transposed) + _Eval(b.Rotated270),
                PatternType.DIAGONAL => _Eval(b.Original) + _Eval(b.HorizontalMirrored),
                _ => throw new NotImplementedException(),
            };
        }

        public float EvalTrainingByPEXTHashing(Boards b)
        {
            float[] e = StageBasedEvaluations[GetStage(b.Original)];
            float _Eval(in Board borad) => e[Hasher.HashByPEXT(borad, NAry)];

            return Type switch
            {
                PatternType.X_SYMETRIC => _Eval(b.Original) + _Eval(b.HorizontalMirrored) + _Eval(b.Transposed) + _Eval(b.Rotated90),
                PatternType.XY_SYMETRIC => _Eval(b.Original) + _Eval(b.HorizontalMirrored) + _Eval(b.Transposed) + _Eval(b.Rotated270),
                PatternType.DIAGONAL => _Eval(b.Original) + _Eval(b.HorizontalMirrored),
                _ => throw new NotImplementedException(),
            };
        }

        public int FlipStone(int hash)
        {
            switch (NAry)
            {
                case NAry.BIN:
                    return (hash >> Hasher.HashLength) | (hash & (1 << Hasher.HashLength - 1));

                case NAry.TER:
                    int result = 0;

                    for (int i = 0; i < Hasher.HashLength; i++)
                    {
                        int s = hash % 3;
                        hash /= 3;
                        s = s == 0 ? 0 : (s == 1 ? 2 : 1);
                        result += s * BinTerUtil.POW3_TABLE[i];
                    }
                    return result;

                default:
                    throw new NotImplementedException();
            }
        }

        protected int ConvertToArrayIndex(int i)
        {
            switch (NAry)
            {
                case NAry.BIN:
                    (int b1, int b2) = BinTerUtil.ConvertTerToBinPair(i, Hasher.HashLength);
                    return b1 | (b2 << Hasher.HashLength);

                case NAry.TER:
                    return i;

                default:
                    throw new NotImplementedException();
            }
        }

        public void Load()
        {
            if (!File.Exists(FilePath))
            {
            }

            using var reader = new BinaryReader(new FileStream(FilePath, FileMode.Open));

            static byte ConvertToInt8(float e)
            {
                return (byte)Math.Clamp(e * 32 + 128, 0, 255);
            }

            for (int stage = 0; stage < STAGES; stage++)
            {
                for (int i = 0; i < NumOfStates; i++)
                {
                    float e = reader.ReadSingle();

                    int index = ConvertToArrayIndex(i);
                    StageBasedEvaluations[stage][index] = e;
                    StageBasedEvaluationsB[stage][index] = ConvertToInt8(e);
                }
            }
        }

        public void Save()
        {
            using var writer = new BinaryWriter(new FileStream(FilePath, FileMode.Create));

            for (int stage = 0; stage < STAGES; stage++)
            {
                for (int i = 0; i < NumOfStates; i++)
                {
                    int index = ConvertToArrayIndex(i);
                    writer.Write(StageBasedEvaluations[stage][index]);
                }
            }
        }

        public bool Test()
        {
            return Enumerable.Range(0, STAGES).Select(ConvertToArrayIndex).All(i => Hasher.HashByPEXT(Hasher.FromHash(i), NAry) == i);
        }

        public void Info(int stage, float threshold)
        {
            for(int i = 0; i < NumOfStates; i++)
            {
                int index = ConvertToArrayIndex(i);

                if (StageBasedEvaluations[stage][index] > threshold)
                    InfoHash(stage, index);
            }
        }

        public void InfoHash(int stage, int hash)
        {
            Console.WriteLine(Hasher.FromHash(hash));
            Console.WriteLine($"Stage : {stage}, Hash : {hash}");
            Console.WriteLine($"Eval : {StageBasedEvaluations[stage][hash]}");
            Console.WriteLine();
        }
    }
}
