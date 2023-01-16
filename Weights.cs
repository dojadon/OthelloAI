using System;
using System.Collections.Generic;
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

        public int Start { get; } = 10;
        public int End { get; } = 51;

        public WeightsStagebased(Weight[] weights)
        {
            Weights = weights;
        }

        public int GetStage(int n_stone, int n_div)
        {
            return (Math.Clamp(n_stone, Start, End - 1) - Start) / (End / n_div);
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

    public abstract class WeightsArray : Weight
    {
        public float[] weights;
        byte[] weights_b;

        public override float[] GetWeights() => weights;

        public int NumOfStates { get; }
        public int HashLength { get; }
        public abstract int ArrayLength { get; }

        public WeightsArray(int length)
        {
            HashLength = length;
            NumOfStates = (int)Math.Pow(3, length);
            Reset();
        }

        public abstract uint ConvertStateToHash(int state);

        public abstract int FlipHash(int hash);

        public override void Reset()
        {
            weights = new float[ArrayLength];
            weights_b = new byte[ArrayLength];
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
           // int flipped = FlipHash(hash);

            weights[hash] += add;
           // weights[flipped] -= add;

            weights_b[hash] = ConvertToInt8(weights[hash], range);
         //   weights_b[flipped] = ConvertToInt8(weights[flipped], range);
        }

        public override void ApplyTrainedEvaluation(float range)
        {
            for (int i = 0; i < NumOfStates; i++)
            {
                uint index = ConvertStateToHash(i);
                weights_b[index] = ConvertToInt8(weights[index], range);
            }
        }

        public override void Read(BinaryReader reader)
        {
            for (int i = 0; i < NumOfStates; i++)
            {
                float e = reader.ReadSingle();

                uint index = ConvertStateToHash(i);
                weights[index] = e;
                weights_b[index] = ConvertToInt8(weights[index], WEIGHT_RANGE);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            for (int i = 0; i < NumOfStates; i++)
            {
                uint index = ConvertStateToHash(i);

                float e = weights[index];
                writer.Write(e);
            }
        }
    }

    public abstract class WeightArrayTer : WeightsArray
    {
        public WeightArrayTer(int length) : base(length)
        {
        }

        public override int ArrayLength => (int) Math.Pow(3, HashLength);

        public override uint ConvertStateToHash(int state) => (uint) state;

        public override int FlipHash(int hash)
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
    }

    public abstract class WeightArrayBin : WeightsArray
    {
        public WeightArrayBin(int length) : base(length)
        {
        }

        public override int ArrayLength => (int) Math.Pow(2, HashLength * 2);

        public override uint ConvertStateToHash(int state)
        {
            (uint b1, uint b2) = BinTerUtil.ConvertTerToBinPair(state, HashLength);
            return b1 | (b2 << HashLength);
        }

        public override int FlipHash(int hash)
        {
            return (hash >> HashLength) | ((hash & ((1 << HashLength) - 1)) << HashLength);
        }
    }

    public class WeightsArrayScanning : WeightArrayTer
    {
        public ulong Mask { get; }

        readonly int[] pos;

        public static int[] MaskToPositions(ulong mask)
        {
            var list = new List<int>();

            for (int i = 0; i < 64; i++)
            {
                if (((mask >> i) & 1) != 0)
                    list.Add(i);
            }
            list.Reverse();
            return list.ToArray();
        }

        public WeightsArrayScanning(ulong m) : base(Board.BitCount(m))
        {
            Mask = m;
            pos = MaskToPositions(m);
        }

        public override Weight Copy()
        {
            return new WeightsArrayScanning(Mask);
        }

        public override int Hash(Board board)
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
    }

    public class WeightArrayPextHashingTer : WeightArrayTer
    {
        readonly ulong mask;
        readonly int hash_length;

        public WeightArrayPextHashingTer(ulong m) : base(Board.BitCount(m))
        {
            mask = m;
            hash_length = Board.BitCount(mask);
        }

        public override Weight Copy()
        {
            return new WeightArrayPextHashingTer(mask);
        }

        public override int Hash(Board b)
        {
            int hash1 = BinTerUtil.ConvertBinToTer((int) Bmi2.X64.ParallelBitExtract(b.bitB, mask), hash_length);
            int hash2 = BinTerUtil.ConvertBinToTer((int) Bmi2.X64.ParallelBitExtract(b.bitW, mask), hash_length);
            return hash1 + hash2 * 2;
        }
    }

    public class WeightArrayPextHashingBin : WeightArrayBin
    {
        readonly ulong mask;
        readonly int hash_length;

        public WeightArrayPextHashingBin(ulong m) : base(Board.BitCount(m))
        {
            mask = m;
            hash_length = Board.BitCount(mask);
        }

        public override Weight Copy()
        {
            return new WeightArrayPextHashingBin(mask);
        }

        public override int Hash(Board b)
        {
            return (int)(Bmi2.X64.ParallelBitExtract(b.bitB, mask) | (Bmi2.X64.ParallelBitExtract(b.bitW, mask) << hash_length));
        }
    }
}
