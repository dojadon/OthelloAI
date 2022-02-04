using System;
using System.IO;

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
        //protected NAry NAry { get; }
        public BoardHasher Hasher { get; }

        public int ArrayLength { get; }
        public int NumOfStates { get; }

        protected int[][] StageBasedGameCount { get; } = new int[STAGES][];
        protected int[][] StageBasedWinCount { get; } = new int[STAGES][];

        protected float[][] StageBasedEvaluations { get; } = new float[STAGES][];
        protected byte[][] StageBasedEvaluationsB { get; } = new byte[STAGES][];

        public Pattern(string filePath, BoardHasher hasher, PatternType type)
        {
            FilePath = filePath;
            Hasher = hasher;
            Type = type;

            NumOfStates = (int)Math.Pow(3, Hasher.HashLength);

#if BIN_HASH
            ArrayLength = (int)Math.Pow(2, 2 * Hasher.HashLength);
#else
            ArrayLength = NumOfStates;
#endif

            for (int i = 0; i < STAGES; i++)
            {
                StageBasedGameCount[i] = new int[ArrayLength];
                StageBasedWinCount[i] = new int[ArrayLength];
                StageBasedEvaluations[i] = new float[ArrayLength];
                StageBasedEvaluationsB[i] = new byte[ArrayLength];
            }
        }

        protected int GetStage(Board board)
        {
            return board.n_stone - 5;
        }

        public void UpdateWinCount(Boards boards, bool win, int weight)
        {
            switch (Type)
            {
                case PatternType.X_SYMETRIC:
                    UpdateWinCount(boards.Original, win, weight);
                    UpdateWinCount(boards.HorizontalMirrored, win, weight);
                    UpdateWinCount(boards.Transposed, win, weight);
                    UpdateWinCount(boards.Rotated90, win, weight);
                    break;

                case PatternType.XY_SYMETRIC:
                    UpdateWinCount(boards.Original, win, weight);
                    UpdateWinCount(boards.HorizontalMirrored, win, weight);
                    UpdateWinCount(boards.Transposed, win, weight);
                    UpdateWinCount(boards.Rotated270, win, weight);
                    break;

                case PatternType.DIAGONAL:
                    UpdateWinCount(boards.Original, win, weight);
                    UpdateWinCount(boards.HorizontalMirrored, win, weight);
                    break;
            }
        }

        public void UpdateWinCount(Board board, bool win, int weight)
        {
            int stage = GetStage(board);

            int hash = Hasher.HashByPEXT(board);
            int flipped = FlipHash(hash);

            StageBasedGameCount[stage][hash] += weight;
            StageBasedGameCount[stage][flipped] += weight;

            if (win)
                StageBasedWinCount[stage][hash] += weight;
            else
                StageBasedWinCount[stage][flipped] += weight;
        }

        public void UpdataEvaluation(Boards boards, float add)
        {
            switch (Type)
            {
                case PatternType.X_SYMETRIC:
                    UpdataEvaluation(boards.Original, add);
                    UpdataEvaluation(boards.HorizontalMirrored, add);
                    UpdataEvaluation(boards.Transposed, add);
                    UpdataEvaluation(boards.Rotated270, add);
                    break;

                case PatternType.XY_SYMETRIC:
                    UpdataEvaluation(boards.Original, add);
                    UpdataEvaluation(boards.HorizontalMirrored, add);
                    UpdataEvaluation(boards.Transposed, add);
                    UpdataEvaluation(boards.Rotated90, add);
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

            int hash = Hasher.HashByPEXT(board);
            int flipped = FlipHash(hash);

            StageBasedEvaluations[stage][hash] += add;
            StageBasedEvaluations[stage][flipped] -= add;
        }

        public int EvalByPEXTHashing(Boards b)
        {
            byte[] e = StageBasedEvaluationsB[GetStage(b.Original)];
            byte _Eval(in Board borad) => e[Hasher.HashByPEXT(borad)];

            return Type switch
            {
                PatternType.X_SYMETRIC => _Eval(b.Original) + _Eval(b.HorizontalMirrored) + _Eval(b.Transposed) + _Eval(b.Rotated90) - 128 * 4,
                PatternType.XY_SYMETRIC => _Eval(b.Original) + _Eval(b.HorizontalMirrored) + _Eval(b.Transposed) + _Eval(b.Rotated270) - 128 * 4,
                PatternType.DIAGONAL => _Eval(b.Original) + _Eval(b.HorizontalMirrored) - 128 * 2,
                _ => throw new NotImplementedException(),
            };
        }

        public float EvalTrainingByPEXTHashing(Boards b)
        {
            float[] e = StageBasedEvaluations[GetStage(b.Original)];
            float _Eval(in Board borad) => e[Hasher.HashByPEXT(borad)];

            return Type switch
            {
                PatternType.X_SYMETRIC => _Eval(b.Original) + _Eval(b.HorizontalMirrored) + _Eval(b.Transposed) + _Eval(b.Rotated90),
                PatternType.XY_SYMETRIC => _Eval(b.Original) + _Eval(b.HorizontalMirrored) + _Eval(b.Transposed) + _Eval(b.Rotated270),
                PatternType.DIAGONAL => _Eval(b.Original) + _Eval(b.HorizontalMirrored),
                _ => throw new NotImplementedException(),
            };
        }

        protected int ConvertStateToHash(int i) => Hasher.ConvertStateToHash(i);

        protected Board FromHash(int hash) => Hasher.FromHash(hash);

        public int FlipHash(int hash) => Hasher.FlipHash(hash);

        public void Load2()
        {
            using var reader = new BinaryReader(new FileStream("e2\\" + FilePath, FileMode.Open));

            static byte ConvertToInt8(float e)
            {
                return (byte)Math.Clamp(128 + (e - 0.5F) * 255, 0, 255);
            }

            for (int stage = 0; stage < STAGES; stage++)
            {
                for (int i = 0; i < NumOfStates; i++)
                {
                    int game = reader.ReadInt32();
                    int win = reader.ReadInt32();

                    float e = game > 10 ? (float)win / game : 0.5F;

                    int index = ConvertStateToHash(i);
                    StageBasedEvaluations[stage][index] = e;
                    StageBasedEvaluationsB[stage][index] = ConvertToInt8(e);
                }
            }
        }

        public void Load()
        {
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

                    int index = ConvertStateToHash(i);
                    StageBasedEvaluations[stage][index] = e;
                    StageBasedEvaluationsB[stage][index] = ConvertToInt8(e);
                }
            }
        }

        public void Save2()
        {
            using var writer = new BinaryWriter(new FileStream("e2\\" + FilePath, FileMode.Create));

            for (int stage = 0; stage < STAGES; stage++)
            {
                for (int i = 0; i < NumOfStates; i++)
                {
                    int index = ConvertStateToHash(i);
                    writer.Write(StageBasedGameCount[stage][index]);
                    writer.Write(StageBasedWinCount[stage][index]);
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
                    int index = ConvertStateToHash(i);
                    writer.Write(StageBasedEvaluations[stage][index]);
                }
            }
        }

        public bool Test()
        {
            for (int i = 0; i < NumOfStates; i++)
            {
                int hash = ConvertStateToHash(i);

                if (Hasher.HashByPEXT(FromHash(hash)) != hash)
                {
                    Console.WriteLine(FromHash(hash));
                    Console.WriteLine($"{hash}, {Hasher.HashByPEXT(FromHash(hash))}");
                    Console.WriteLine($"{hash}, {(hash >> Hasher.HashLength)}, {(hash & ((1 << Hasher.HashLength) - 1)) << Hasher.HashLength}");
                    return false;
                }
            }
            return true;
        }

        public void TestRotation()
        {
            var br = new Board(0b10101010_01010101_10101010_01010101_10101010_01010101_10101010_01010101UL,
                    0b01010101_10101010_01010101_10101010_01010101_10101010_01010101_10101010UL);
            var b = Hasher.FromTerHash(Hasher.HashTerByPEXT(br));
            var bs = new Boards(b);

            Console.WriteLine(bs.Original);
            Console.WriteLine(bs.HorizontalMirrored);
            Console.WriteLine(bs.Transposed);
            Console.WriteLine(bs.Rotated90);
            Console.WriteLine(bs.Rotated270);
        }

        public void Info(int stage, float threshold)
        {
            for (int i = 0; i < NumOfStates; i++)
            {
                int index = ConvertStateToHash(i);

                if (StageBasedEvaluations[stage][index] > threshold)
                    InfoHash(stage, index);
            }
        }

        public void InfoHash(int stage, int hash)
        {
            Console.WriteLine(FromHash(hash));
            Console.WriteLine($"Stage : {stage}, Hash : {hash}");
            Console.WriteLine($"Eval : {StageBasedEvaluations[stage][hash]}");
            Console.WriteLine();
        }
    }
}
