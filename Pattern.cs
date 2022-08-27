using System;
using System.Collections.Generic;
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

    public class Pattern
    {
        public int NumStages { get; }

        public string FilePath { get; }

        public PatternType Type { get; }
        public BoardHasher Hasher { get; }

        public int ArrayLength { get; }
        public int NumOfStates { get; }

        public float[][] StageBasedEvaluations { get; private set; }
        protected byte[][] StageBasedEvaluationsB { get; private set; }

        public float Variance { get; set; }

        public Pattern(string filePath, int n_stages, BoardHasher hasher, PatternType type)
        {
            FilePath = filePath;
            NumStages = n_stages;
            Hasher = hasher;
            Type = type;

            NumOfStates = (int)Math.Pow(3, Hasher.HashLength);

#if BIN_HASH
            ArrayLength = (int)Math.Pow(2, 2 * Hasher.HashLength);
#else
            ArrayLength = NumOfStates;
#endif

            Reset();
        }

        public void Reset()
        {
            StageBasedEvaluations = new float[NumStages][];
            StageBasedEvaluationsB = new byte[NumStages][];

            for (int i = 0; i < NumStages; i++)
            {
                StageBasedEvaluations[i] = new float[ArrayLength];
                StageBasedEvaluationsB[i] = new byte[ArrayLength];
            }
        }

        protected int GetStage(Board board)
        {
            return (board.n_stone - 5) / (60 / NumStages);
        }

        public void UpdataEvaluation(Board board, float add, float range)
        {
            int stage = GetStage(board);

            int hash = Hasher.HashByPEXT(board);
            int flipped = Hasher.FlipHash(hash);

            StageBasedEvaluations[stage][hash] += add;
            StageBasedEvaluations[stage][flipped] -= add;

            StageBasedEvaluationsB[stage][hash] = ConvertToInt8(StageBasedEvaluationsB[stage][hash] + add, range);
            StageBasedEvaluationsB[stage][flipped] = ConvertToInt8(StageBasedEvaluationsB[stage][flipped] - add, range);
        }

        byte ConvertToInt8(float x, float v)
        {
            return (byte)Math.Clamp(x / v * 127 + 128, 0, 255);
        }

        public void ApplyTrainedEvaluation()
        {
            float range = StageBasedEvaluations.SelectMany(a => a.Select(Math.Abs).Where(f => f < 10).OrderByDescending(f => f).Take(10)).Average();

            for (int stage = 0; stage < NumStages; stage++)
            {
                for (int i = 0; i < NumOfStates; i++)
                {
                    uint index = Hasher.ConvertStateToHash(i);
                    StageBasedEvaluationsB[stage][index] = ConvertToInt8(StageBasedEvaluations[stage][index], range);
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
            for (int stage = 0; stage < NumStages; stage++)
            {
                for (int i = 0; i < NumOfStates; i++)
                {
                    float e = reader.ReadSingle();

                    uint index = Hasher.ConvertStateToHash(i);
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
            for (int stage = 0; stage < NumStages; stage++)
            {
                for (int i = 0; i < NumOfStates; i++)
                {
                    uint index = Hasher.ConvertStateToHash(i);
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
                uint hash = Hasher.ConvertStateToHash(i);

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
                uint index = Hasher.ConvertStateToHash(i);

                if (StageBasedEvaluations[stage][index] > threshold)
                    InfoHash(stage, index);
            }
        }

        public void InfoHash(int stage, uint hash)
        {
            Console.WriteLine(Hasher.FromHash(hash));
            Console.WriteLine($"Stage : {stage}, Hash : {hash}");
            Console.WriteLine($"Eval : {StageBasedEvaluations[stage][hash]}");
            Console.WriteLine();
        }

        public static void Test(float[][] src, float[][] dst, ulong src_t, ulong dst_t)
        {
            ulong and = src_t & dst_t;
            int n = Board.BitCount(src_t);

            foreach(uint i in Test(and, n))
            {
                uint[] indices1 = Test(src_t & ~dst_t, n).ToArray();
                float[] e = src.Select(a => indices1.Select(j => a[i | j]).Average()).ToArray();

                uint[] indices2 = Test(~src_t & dst_t, n).ToArray();

                for(int stage = 0; stage < dst.Length; stage++)
                    foreach(uint j in indices2)
                        dst[stage][i | j] += e[stage] * 0.5F;
            }
        }

        public static IEnumerable<uint> Test(ulong mask, int n_src)
        {
            int n_mask = Board.BitCount(mask);

            return Enumerable.Range(0, (int)Math.Pow(3, n_mask)).Select(i =>
            {
                (uint i1, uint i2) = BinTerUtil.ConvertTerToBinPair(i, n_mask);
                uint u1 = System.Runtime.Intrinsics.X86.Bmi2.ParallelBitDeposit(i1, (uint)mask);
                uint u2 = System.Runtime.Intrinsics.X86.Bmi2.ParallelBitDeposit(i2, (uint)mask);
                return u2 << n_src | u1;
            });
        }
    }
}
