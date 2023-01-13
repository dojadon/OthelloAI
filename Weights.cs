using System;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.X86;

namespace OthelloAI
{
    public abstract class Weight
    {
        public const float WEIGHT_RANGE = 10;

        public abstract void Reset();
        public abstract int Eval(RotatedAndMirroredBoards b);
        public abstract float EvalTraining(RotatedAndMirroredBoards b);
        public abstract int NumOfEvaluation(int n_discs);

        public abstract void UpdataEvaluation(Board board, float add, float range);
        public abstract void ApplyTrainedEvaluation(float range);

        public abstract float[] GetWeights();

        public abstract void Read(BinaryReader reader);
        public abstract void Write(BinaryWriter writer);

        public abstract Weight Copy();

        public static byte ConvertToInt8(float x, float range)
        {
            return (byte)Math.Clamp(x / range * 127 + 128, 0, 255);
        }

        public void Load(string path)
        {
            using var reader = new BinaryReader(new FileStream(path, FileMode.Open));
            Read(reader);
        }

        public void Save(string path)
        {
            using var writer = new BinaryWriter(new FileStream(path, FileMode.Create));
            Write(writer);
        }
    }

    public class WeightsSum : Weight
    {
        public Weight[] Weights { get; }

        public WeightsSum(params Weight[] weights)
        {
            Weights = weights;
        }

        public override float[] GetWeights()
        {
            return Weights.SelectMany(w => w.GetWeights()).ToArray();
        }

        public override void Reset()
        {
            foreach (var w in Weights)
                w.Reset();
        }

        public override void UpdataEvaluation(Board board, float add, float range)
        {
            foreach (var w in Weights)
                w.UpdataEvaluation(board, add, range);
        }

        public override int Eval(RotatedAndMirroredBoards b)
        {
            return Weights.Sum(w => w.Eval(b));
        }

        public override float EvalTraining(RotatedAndMirroredBoards b)
        {
            return Weights.Sum(w => w.EvalTraining(b));
        }

        public override int NumOfEvaluation(int n_discs)
        {
            return Weights.Sum(w => w.NumOfEvaluation(n_discs));
        }

        public override void ApplyTrainedEvaluation(float range)
        {
            foreach (var w in Weights)
                w.ApplyTrainedEvaluation(range);
        }

        public override void Read(BinaryReader reader)
        {
            foreach (var w in Weights)
                w.Read(reader);
        }

        public override void Write(BinaryWriter writer)
        {
            foreach (var w in Weights)
                w.Write(writer);
        }

        public override Weight Copy()
        {
            return new WeightsSum(Weights.Select(w => w.Copy()).ToArray());
        }
    }

    public class WeightsStagebased : Weight
    {
        Weight[] Weights { get; }

        public WeightsStagebased(Weight[] weights)
        {
            Weights = weights;
        }

        public static int GetStage(int n_stone, int n_div)
        {
            return (n_stone - 5) / (60 / n_div);
        }

        protected Weight GetCurrentWeights(Board board)
        {
            return GetCurrentWeights(board.n_stone);
        }

        protected Weight GetCurrentWeights(int n_discs)
        {
            return Weights[GetStage(n_discs, Weights.Length)];
        }

        public override float[] GetWeights()
        {
            return Weights.SelectMany(w => w.GetWeights()).ToArray();
        }

        public override void Reset()
        {
            foreach (var w in Weights)
                w.Reset();
        }

        public override void UpdataEvaluation(Board board, float add, float range)
        {
            GetCurrentWeights(board).UpdataEvaluation(board, add, range);
        }

        public override int Eval(RotatedAndMirroredBoards b)
        {
            return GetCurrentWeights(b.rot0).Eval(b);
        }

        public override float EvalTraining(RotatedAndMirroredBoards b)
        {
            return GetCurrentWeights(b.rot0).EvalTraining(b);
        }

        public override int NumOfEvaluation(int n_discs)
        {
            return GetCurrentWeights(n_discs).NumOfEvaluation(n_discs);
        }

        public override void ApplyTrainedEvaluation(float range)
        {
            foreach (var w in Weights)
                w.ApplyTrainedEvaluation(range);
        }

        public override void Read(BinaryReader reader)
        {
            foreach (var w in Weights)
                w.Read(reader);
        }

        public override void Write(BinaryWriter writer)
        {
            foreach (var w in Weights)
                w.Write(writer);
        }

        public override Weight Copy()
        {
            return new WeightsStagebased(Weights.Select(w => w.Copy()).ToArray());
        }
    }

    public enum HashType
    {
        BIN, TER
    }

    public abstract class WeightsArray : Weight
    {
        public BoardHasher Hasher { get; }

        public ulong Mask { get; }

        public float[] weights;
        byte[] weights_b;

        readonly int[] pos;

        public abstract HashType Type { get; }

        public override float[] GetWeights() => weights;

        public int NumOfStates { get; }
        public int HashLength { get; }

        public WeightsArray(int length)
        {
            HashLength = length;
            NumOfStates = (int) Math.Pow(3, length);
            Reset();
        }

        public override Weight Copy()
        {
            return new WeightsArrayS(Mask);
        }

        public override void Reset()
        {
            int length = Type switch
            {
                HashType.BIN => Math.Pow(2, HashLength * 2),
                HashType.TER => Math.Pow(3, HashLength),
            };

            weights = new float[length];
            weights_b = new byte[length];
        }

        public abstract int Hash(Board board);

        public int Eval(Board b)
        {
            return weights_b[Hash(b)];
        }

        public override int Eval(RotatedAndMirroredBoards b)
        {
            return Eval(b.rot0) + Eval(b.rot90) + Eval(b.rot180) + Eval(b.rot270)
                + Eval(b.inv_rot0) + Eval(b.inv_rot90) + Eval(b.inv_rot180) + Eval(b.inv_rot270) - 128 * 8;
        }

        public float EvalTraining(Board b)
        {
            return weights[Hash(b)];
        }

        public override float EvalTraining(RotatedAndMirroredBoards b)
        {
            return EvalTraining(b.rot0) + EvalTraining(b.rot90) + EvalTraining(b.rot180) + EvalTraining(b.rot270)
                + EvalTraining(b.inv_rot0) + EvalTraining(b.inv_rot90) + EvalTraining(b.inv_rot180) + EvalTraining(b.inv_rot270);
        }

        public override int NumOfEvaluation(int n_discs) => 8;

        public override void UpdataEvaluation(Board board, float add, float range)
        {
            int hash = Hash(board);

            weights[hash] += add;
            weights_b[hash] = ConvertToInt8(weights[hash], range);
        }

        public override void ApplyTrainedEvaluation(float range)
        {
            for (int i = 0; i < NumOfStates; i++)
            {
                uint index = Hasher.ConvertStateToHash(i);
                weights_b[index] = ConvertToInt8(weights[index], range);
            }
        }

        public override void Read(BinaryReader reader)
        {
            for (int i = 0; i < NumOfStates; i++)
            {
                float e = reader.ReadSingle();

                uint index = Hasher.ConvertStateToHash(i);
                weights[index] = e;
                weights_b[index] = ConvertToInt8(weights[index], WEIGHT_RANGE);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            for (int i = 0; i < NumOfStates; i++)
            {
                uint index = Hasher.ConvertStateToHash(i);

                float e = weights[index];
                writer.Write(e);
            }
        }
    }

    public class WeightsArrayS : Weight
    {
        public BoardHasher Hasher { get; }

        public ulong Mask { get; }

        public float[] weights;
        byte[] weights_b;

        readonly int[] pos;

        public override float[] GetWeights() => weights;

        public int NumOfStates { get; }

        public WeightsArrayS(ulong m)
        {
            Mask = m;

            pos = BoardHasherMask.MaskToPositions(m);
            Hasher = new BoardHasherScanning(pos);
            NumOfStates = Hasher.NumOfStates;

            Reset();
        }

        public override Weight Copy()
        {
            return new WeightsArrayS(Mask);
        }

        public override void Reset()
        {
            weights = new float[Hasher.ArrayLength];
            weights_b = new byte[Hasher.ArrayLength];
        }

        public int Hash(Board board)
        {
            int hash = 0;

            for (int i = 0; i < pos.Length; i++)
            {
                int p = pos[i];

                hash *= 3;
                hash += (int)((board.bitB >> p) & 1);
                hash += (int)((board.bitW >> p) & 1) * 2;
            }
            return hash;
        }

        public int Eval(Board b)
        {
            return weights_b[Hash(b)];
        }

        public override int Eval(RotatedAndMirroredBoards b)
        {
            return Eval(b.rot0) + Eval(b.rot90) + Eval(b.rot180) + Eval(b.rot270)
                + Eval(b.inv_rot0) + Eval(b.inv_rot90) + Eval(b.inv_rot180) + Eval(b.inv_rot270) - 128 * 8;
        }

        public float EvalTraining(Board b)
        {
            return weights[Hash(b)];
        }

        public override float EvalTraining(RotatedAndMirroredBoards b)
        {
            return EvalTraining(b.rot0) + EvalTraining(b.rot90) + EvalTraining(b.rot180) + EvalTraining(b.rot270)
                + EvalTraining(b.inv_rot0) + EvalTraining(b.inv_rot90) + EvalTraining(b.inv_rot180) + EvalTraining(b.inv_rot270);
        }

        public override int NumOfEvaluation(int n_discs) => 8;

        public override void UpdataEvaluation(Board board, float add, float range)
        {
            int hash = Hasher.Hash(board);
            int flipped = Hasher.FlipHash(hash);

            weights[hash] += add;
            weights[flipped] -= add;

            weights_b[hash] = ConvertToInt8(weights[hash], range);
            weights_b[flipped] = ConvertToInt8(weights[flipped], range);
        }

        public override void ApplyTrainedEvaluation(float range)
        {
            for (int i = 0; i < NumOfStates; i++)
            {
                uint index = Hasher.ConvertStateToHash(i);
                weights_b[index] = ConvertToInt8(weights[index], range);
            }
        }

        public override void Read(BinaryReader reader)
        {
            for (int i = 0; i < NumOfStates; i++)
            {
                float e = reader.ReadSingle();

                uint index = Hasher.ConvertStateToHash(i);
                weights[index] = e;
                weights_b[index] = ConvertToInt8(weights[index], WEIGHT_RANGE);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            for (int i = 0; i < NumOfStates; i++)
            {
                uint index = Hasher.ConvertStateToHash(i);

                float e = weights[index];
                writer.Write(e);
            }
        }
    }

    public class WeightsArrayR : Weight
    {
        public BoardHasher Hasher { get; }

        public float[] weights;

        byte[] weights_b;

        readonly ulong mask;

        readonly int hash_length;

        public override float[] GetWeights() => weights;

        public WeightsArrayR(ulong m)
        {
            mask = m;
            hash_length = Board.BitCount(mask);

            Hasher = new BoardHasherMask(mask);

            Reset();
        }

        public override Weight Copy()
        {
            return new WeightsArrayR(mask);
        }

        public override void Reset()
        {
            weights = new float[Hasher.ArrayLength];
            weights_b = new byte[Hasher.ArrayLength];
        }

        public int Eval_(Board b)
        {
            ulong idx = Bmi2.X64.ParallelBitExtract(b.bitB, mask) | (Bmi2.X64.ParallelBitExtract(b.bitW, mask) << hash_length);
            return weights_b[idx];
        }

        public override int Eval(RotatedAndMirroredBoards b)
        {
            return Eval_(b.rot0) + Eval_(b.inv_rot0) + Eval_(b.rot90) + Eval_(b.inv_rot90)
                + Eval_(b.rot180) + Eval_(b.inv_rot180) + Eval_(b.rot270) + Eval_(b.inv_rot270) - 128 * 8;
        }

        public float EvalTraining_(Board b)
        {
            return weights[Hasher.Hash(b)];
        }

        public override float EvalTraining(RotatedAndMirroredBoards b)
        {
            return EvalTraining_(b.rot0) + EvalTraining_(b.inv_rot0) + EvalTraining_(b.rot90) + EvalTraining_(b.inv_rot90) +
                EvalTraining_(b.rot180) + EvalTraining_(b.inv_rot180) + EvalTraining_(b.rot270) + EvalTraining_(b.inv_rot270);
        }

        public override int NumOfEvaluation(int n_discs) => 8;

        public override void UpdataEvaluation(Board board, float add, float range)
        {
            int hash = Hasher.Hash(board);
            Update(hash, add, range);
        }

        public void Update(int hash, float add, float range)
        {
            int flipped = Hasher.FlipHash(hash);

            weights[hash] += add;
            weights[flipped] -= add;

            weights_b[hash] = ConvertToInt8(weights[hash], range);
            weights_b[flipped] = ConvertToInt8(weights[flipped], range);
        }

        public override void ApplyTrainedEvaluation(float range)
        {
            for (int i = 0; i < Hasher.NumOfStates; i++)
            {
                uint index = Hasher.ConvertStateToHash(i);
                weights_b[index] = ConvertToInt8(weights[index], range);
            }
        }

        public override void Read(BinaryReader reader)
        {
            for (int i = 0; i < Hasher.NumOfStates; i++)
            {
                float e = reader.ReadSingle();

                uint index = Hasher.ConvertStateToHash(i);
                weights[index] = e;
                weights_b[index] = ConvertToInt8(weights[index], WEIGHT_RANGE);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            for (int i = 0; i < Hasher.NumOfStates; i++)
            {
                uint index = Hasher.ConvertStateToHash(i);

                float e = weights[index];
                writer.Write(e);
            }
        }
    }
}
