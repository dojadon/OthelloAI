using System;
using System.IO;
using System.Linq;

namespace OthelloAI
{
    public abstract class PatternWeights
    {
        public abstract void Reset();

        public abstract int Eval(Board b);
        public abstract float EvalTraining(Board b);
        public abstract void UpdataEvaluation(Board board, float add, float range);
        public abstract void ApplyTrainedEvaluation(float range);

        public abstract float[] GetWeights();

        public abstract void Read(BinaryReader reader);
        public abstract void Write(BinaryWriter writer);

        public byte ConvertToInt8(float x, float range)
        {
            return (byte)Math.Clamp(x / range * 127 + 128, 0, 255);
        }
    }

    public class PatternWeightsStagebased : PatternWeights
    {
        PatternWeights[] Weights { get; }

        protected int GetStage(Board board)
        {
            return (board.n_stone - 5) / (60 / Weights.Length);
        }

        protected PatternWeights GetCurrentWeights(Board board)
        {
            return Weights[GetStage(board)];
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

        public override int Eval(Board b)
        {
            return GetCurrentWeights(b).Eval(b);
        }

        public override float EvalTraining(Board b)
        {
            return GetCurrentWeights(b).EvalTraining(b);
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
    }

    public class PatternWeightsArray : PatternWeights
    {
        public BoardHasher Hasher { get; }

        public float[] Weights { get; private set; }
        protected byte[] WeightsB { get; private set; }

        public override float[] GetWeights() => Weights;

        public int NumOfStates { get; }

        public PatternWeightsArray(BoardHasher hasher)
        {
            Hasher = hasher;
            NumOfStates = Hasher.NumOfStates;
        }

        public override void Reset()
        {
            Weights = new float[NumOfStates];
            WeightsB = new byte[NumOfStates];
        }

        public override int Eval(Board b)
        {
            return WeightsB[Hasher.Hash(b)];
        }

        public override float EvalTraining(Board b)
        {
            return Weights[Hasher.Hash(b)];
        }

        public override void UpdataEvaluation(Board board, float add, float range)
        {
            int hash = Hasher.Hash(board);
            int flipped = Hasher.FlipHash(hash);

            Weights[hash] += add;
            Weights[flipped] -= add;

            WeightsB[hash] = ConvertToInt8(Weights[hash], range);
            WeightsB[flipped] = ConvertToInt8(Weights[flipped], range);
        }

        public override void ApplyTrainedEvaluation(float range)
        {
            for (int i = 0; i < NumOfStates; i++)
            {
                uint index = Hasher.ConvertStateToHash(i);
                WeightsB[index] = ConvertToInt8(Weights[index], range);
            }
        }

        public override void Read(BinaryReader reader)
        {
            for (int i = 0; i < NumOfStates; i++)
            {
                float e = reader.ReadSingle();

                uint index = Hasher.ConvertStateToHash(i);
                Weights[index] = e;
            }
        }

        public override void Write(BinaryWriter writer)
        {
            for (int i = 0; i < NumOfStates; i++)
            {
                uint index = Hasher.ConvertStateToHash(i);

                float e = Weights[index];
                writer.Write(e);
            }
        }
    }

    public class PatternWeights2Disc : PatternWeights
    {
        public ulong mask1, mask2;
        public float w_xx, w_xy, w_x0, w_0x;
        public byte b_xx, b_xy, b_x0, b_0x;

        public override float[] GetWeights() => new float[] { w_xx, w_xy, w_x0, w_0x };

        public override int Eval(Board b)
        {
            if ((b.bitB & mask1) != 0)
            {
                if ((b.bitB & mask2) != 0)
                    return b_xx;
                else if ((b.bitW & mask2) != 0)
                    return b_xy;
                else
                    return b_x0;
            }
            else if ((b.bitW & mask1) != 0)
            {
                if ((b.bitB & mask2) != 0)
                    return -b_xy;
                else if ((b.bitW & mask2) != 0)
                    return -b_xx;
                else
                    return -b_x0;
            }
            else
            {
                if ((b.bitB & mask2) != 0)
                    return b_0x;
                else if ((b.bitW & mask2) != 0)
                    return -b_0x;
                else
                    return 0;
            }
        }

        public override float EvalTraining(Board b)
        {
            if ((b.bitB & mask1) != 0)
            {
                if ((b.bitB & mask2) != 0)
                    return w_xx;
                else if ((b.bitW & mask2) != 0)
                    return w_xy;
                else
                    return w_x0;
            }
            else if ((b.bitW & mask1) != 0)
            {
                if ((b.bitB & mask2) != 0)
                    return -w_xy;
                else if ((b.bitW & mask2) != 0)
                    return -w_xx;
                else
                    return -w_x0;
            }
            else
            {
                if ((b.bitB & mask2) != 0)
                    return w_0x;
                else if ((b.bitW & mask2) != 0)
                    return -w_0x;
                else
                    return 0;
            }
        }

        public override void UpdataEvaluation(Board b, float add, float range)
        {
            if ((b.bitB & mask1) != 0)
            {
                if ((b.bitB & mask2) != 0)
                {
                    w_xx += add;
                    b_xx = ConvertToInt8(w_xx, range);
                }
                else if ((b.bitW & mask2) != 0)
                {
                    w_xy += add;
                    b_xy = ConvertToInt8(w_xy, range);
                }
                else
                {
                    w_x0 += add;
                    b_x0 = ConvertToInt8(w_x0, range);
                }
            }
            else if ((b.bitW & mask1) != 0)
            {
                if ((b.bitB & mask2) != 0)
                {
                    w_xy -= add;
                    b_xy = ConvertToInt8(w_xy, range);
                }
                else if ((b.bitW & mask2) != 0)
                {
                    w_xx -= add;
                    b_xx = ConvertToInt8(w_xx, range);
                }
                else
                {
                    w_x0 -= add;
                    b_x0 = ConvertToInt8(w_x0, range);
                }
            }
            else
            {
                if ((b.bitB & mask2) != 0)
                {
                    w_0x += add;
                    b_0x = ConvertToInt8(w_0x, range);
                }
                else if ((b.bitW & mask2) != 0)
                {
                    w_0x -= add;
                    b_0x = ConvertToInt8(w_0x, range);
                }
            }
        }

        public override void ApplyTrainedEvaluation(float range)
        {
            b_xx = ConvertToInt8(w_xx, range);
            b_xy = ConvertToInt8(w_xy, range);
            b_x0 = ConvertToInt8(w_x0, range);
            b_0x = ConvertToInt8(w_0x, range);
        }

        public override void Reset()
        {
            w_xx = w_xy = w_x0 = w_0x = 0;
        }

        public override void Read(BinaryReader reader)
        {
            w_xx = reader.ReadSingle();
            w_xy = reader.ReadSingle();
            w_x0 = reader.ReadSingle();
            w_0x = reader.ReadSingle();
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(w_xx);
            writer.Write(w_xy);
            writer.Write(w_x0);
            writer.Write(w_0x);
        }
    }
}
