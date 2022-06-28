using System;
using System.IO;
using System.Linq;

namespace OthelloAI.Patterns
{
    public enum PatternType
    {
        X_SYMMETRIC,
        XY_SYMMETRIC,
        DIAGONAL,
        ASYMMETRIC
    }

    public class RegionForTraining
    {
        protected string FilePath { get; }

        protected PatternType Type { get; }
        public BoardHasher Hasher { get; }

        protected int[] GameCount { get; }
        protected int[] WinCount { get; }

        public int ArrayLength { get; }
        public int NumOfStates { get; }

        public RegionForTraining(string filePath, BoardHasher hasher, PatternType type)
        {
            FilePath = filePath;
            Hasher = hasher;
            Type = type;

            NumOfStates = (int)Math.Pow(3, Hasher.HashLength);

            GameCount = new int[NumOfStates];
            WinCount = new int[NumOfStates];
        }

        public void UpdateWinCountWithRotatingAndFliping(Board board, int win, int game)
        {
            var inv = board.HorizontalMirrored();
            var inv_rot90 = board.Transposed();
            var inv_rot180 = board.VerticalMirrored();
            var rot90 = inv.Transposed();
            var rot180 = inv_rot180.HorizontalMirrored();
            var rot270 = inv_rot90.HorizontalMirrored();
            var inv_rot270 = rot270.VerticalMirrored();

            UpdateWinCount(board, win, game);
            UpdateWinCount(rot90, win, game);
            UpdateWinCount(rot180, win, game);
            UpdateWinCount(rot270, win, game);
            UpdateWinCount(inv, win, game);
            UpdateWinCount(inv_rot90, win, game);
            UpdateWinCount(inv_rot180, win, game);
            UpdateWinCount(inv_rot270, win, game);
        }

        public void UpdateWinCount(Board board, int win, int game)
        {
            int hash = Hasher.HashByScanning(board);
            int flipped = Hasher.FlipHash(hash);

            GameCount[hash] += game;
            GameCount[flipped] += game;

            WinCount[hash] += win;
            WinCount[flipped] += game - win;
        }

        public void Load()
        {
            using var reader = new BinaryReader(new FileStream(FilePath, FileMode.Open));

            for (int i = 0; i < NumOfStates; i++)
            {
                int win = reader.ReadInt32();
                int game = reader.ReadInt32();

                WinCount[i] = win;
                GameCount[i] = game;
            }
        }

        public void Save()
        {
            using var writer = new BinaryWriter(new FileStream(FilePath, FileMode.Create));

            for (int i = 0; i < NumOfStates; i++)
            {
                writer.Write(WinCount[i]);
                writer.Write(GameCount[i]);
            }
        }
    }

    public class Pattern
    {
        public const int STAGES = 60;

        protected string FilePath { get; }

        protected PatternType Type { get; }
        public BoardHasher Hasher { get; }

        public int ArrayLength { get; }
        public int NumOfStates { get; }

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
                StageBasedEvaluations[i] = new float[ArrayLength];
                StageBasedEvaluationsB[i] = new byte[ArrayLength];
            }
        }

        protected int GetStage(Board board)
        {
            return board.n_stone - 5;
        }

        public void UpdataEvaluation(Board board, float add)
        {
            int stage = GetStage(board);

            int hash = Hasher.HashByPEXT(board);
            int flipped = Hasher.FlipHash(hash);

            StageBasedEvaluations[stage][hash] += add;
            StageBasedEvaluations[stage][flipped] -= add;
        }

        public void ApplyTrainedEvaluation()
        {
            static byte ConvertToInt8(float e)
            {
                return (byte)Math.Clamp(e * 32 + 128, 0, 255);
            }

            for (int stage = 0; stage < STAGES; stage++)
            {
                for (int i = 0; i < NumOfStates; i++)
                {
                    int index = Hasher.ConvertStateToHash(i);
                    StageBasedEvaluationsB[stage][index] = ConvertToInt8(StageBasedEvaluations[stage][index]);
                }
            }
        }

        public int EvalByPEXTHashing(RotatedAndMirroredBoards b)
        {
            byte[] e = StageBasedEvaluationsB[GetStage(b.rot0)];
            byte _Eval(in Board borad) => e[Hasher.HashByPEXT(borad)];

            return Type switch
            {
                PatternType.X_SYMMETRIC => _Eval(b.rot0) + _Eval(b.inv_rot0) + _Eval(b.inv_rot90) + _Eval(b.rot270) - 128 * 4,
                PatternType.XY_SYMMETRIC => _Eval(b.rot0) + _Eval(b.inv_rot0) + _Eval(b.inv_rot90) + _Eval(b.rot90) - 128 * 4,
                PatternType.DIAGONAL => _Eval(b.rot0) + _Eval(b.inv_rot0) - 128 * 2,
                PatternType.ASYMMETRIC => b.Sum(bi => _Eval(bi)),
                _ => throw new NotImplementedException(),
            };
        }

        public float EvalTrainingByPEXTHashing(RotatedAndMirroredBoards b)
        {
            float[] e = StageBasedEvaluations[GetStage(b.rot0)];
            float _Eval(in Board borad) => e[Hasher.HashByPEXT(borad)];

            return Type switch
            {
                PatternType.X_SYMMETRIC => _Eval(b.rot0) + _Eval(b.inv_rot0) + _Eval(b.inv_rot90) + _Eval(b.rot270),
                PatternType.XY_SYMMETRIC => _Eval(b.rot0) + _Eval(b.inv_rot0) + _Eval(b.inv_rot90) + _Eval(b.rot90),
                PatternType.DIAGONAL => _Eval(b.rot0) + _Eval(b.inv_rot0),
                PatternType.ASYMMETRIC => b.Sum(bi => _Eval(bi)),
                _ => throw new NotImplementedException(),
            };
        }

        public void Read(BinaryReader reader)
        {
            for (int stage = 0; stage < STAGES; stage++)
            {
                for (int i = 0; i < NumOfStates; i++)
                {
                    float e = reader.ReadSingle();

                    int index = Hasher.ConvertStateToHash(i);
                    StageBasedEvaluations[stage][index] = e;
                }
            }

            ApplyTrainedEvaluation();
        }

        public void Load()
        {
            using var reader = new BinaryReader(new FileStream(FilePath, FileMode.Open));
            Read(reader);
        }

        public void Write(BinaryWriter writer)
        {
            for (int stage = 0; stage < STAGES; stage++)
            {
                for (int i = 0; i < NumOfStates; i++)
                {
                    int index = Hasher.ConvertStateToHash(i);
                    writer.Write(StageBasedEvaluations[stage][index]);
                }
            }
        }

        public void Save()
        {
            using var writer = new BinaryWriter(new FileStream(FilePath, FileMode.Create));
            Write(writer);
        }

        public bool Test()
        {
            for (int i = 0; i < NumOfStates; i++)
            {
                int hash = Hasher.ConvertStateToHash(i);

                if (Hasher.HashByPEXT(Hasher.FromHash(hash)) != hash)
                {
                    Console.WriteLine(Hasher.FromHash(hash));
                    Console.WriteLine($"{hash}, {Hasher.HashByPEXT(Hasher.FromHash(hash))}");
                    Console.WriteLine($"{hash}, {(hash >> Hasher.HashLength)}, {(hash & ((1 << Hasher.HashLength) - 1)) << Hasher.HashLength}");
                    return false;
                }
            }
            return true;
        }

        public void Info(int stage, float threshold)
        {
            for (int i = 0; i < NumOfStates; i++)
            {
                int index = Hasher.ConvertStateToHash(i);

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
