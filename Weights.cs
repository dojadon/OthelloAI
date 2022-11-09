using System;
using System.IO;
using System.Linq;

namespace OthelloAI
{
    public abstract class Weights
    {
        public const float WEIGHT_RANGE = 10;

        public static Weights Create(BoardHasher hasher, int n_stages, string file = "", bool load = false)
        {
            Weights[] weights_stage = Enumerable.Range(0, n_stages).Select(_ => new WeightsArray(hasher)).ToArray();
            var weights = new WeightsStagebased(weights_stage)
            {
                FilePath = file,
            };

            if (load)
                weights.Load();

            return weights;
        }

        public string FilePath { get; set; }

        public abstract void Reset();
        public abstract int Eval(RotatedAndMirroredBoards b);
        public abstract float EvalTraining(RotatedAndMirroredBoards b);
        public abstract int NumOfEvaluation(int n_discs);

        public abstract void UpdataEvaluation(Board board, float add, float range);
        public abstract void ApplyTrainedEvaluation(float range);

        public abstract float[] GetWeights();

        public abstract void Read(BinaryReader reader);
        public abstract void Write(BinaryWriter writer);

        public static byte ConvertToInt8(float x, float range)
        {
            return (byte)Math.Clamp(x / range * 127 + 128, 0, 255);
        }

        public void Load()
        {
            using var reader = new BinaryReader(new FileStream(FilePath, FileMode.Open));
            Read(reader);
        }

        public void Save()
        {
            using var writer = new BinaryWriter(new FileStream(FilePath, FileMode.Create));
            Write(writer);
        }
    }

    public class WeightsSum : Weights
    {
        Weights[] Weights { get; }

        public WeightsSum(params Weights[] weights)
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
    }

    public class WeightsStagebased : Weights
    {
        Weights[] Weights { get; }

        public WeightsStagebased(Weights[] weights)
        {
            Weights = weights;
        }

        public static int GetStage(int n_stone, int n_div)
        {
            return (n_stone - 5) / (60 / n_div);
        }

        protected Weights GetCurrentWeights(Board board)
        {
            return GetCurrentWeights(board.n_stone);
        }

        protected Weights GetCurrentWeights(int n_discs)
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
    }

    public class WeightsArray : Weights
    {
        public BoardHasher Hasher { get; }
        public SymmetricType Type { get; set; }

        public float[] Weights { get; private set; }
        protected byte[] WeightsB { get; private set; }

        public override float[] GetWeights() => Weights;

        public int NumOfStates { get; }

        public WeightsArray(BoardHasher hasher)
        {
            Hasher = hasher;
            NumOfStates = Hasher.NumOfStates;

            Type = Hasher.SymmetricType;

            Reset();
        }

        public override void Reset()
        {
            Weights = new float[Hasher.ArrayLength];
            WeightsB = new byte[Hasher.ArrayLength];
        }

        public int Eval(Board b)
        {
            return WeightsB[Hasher.Hash(b)];
        }

        public override int Eval(RotatedAndMirroredBoards b)
        {
            return Type switch
            {
                SymmetricType.X_SYMMETRIC => Eval(b.rot0) + Eval(b.inv_rot0) + Eval(b.inv_rot90) + Eval(b.rot270) - 128 * 4,
                SymmetricType.XY_SYMMETRIC => Eval(b.rot0) + Eval(b.inv_rot0) + Eval(b.inv_rot90) + Eval(b.rot90) - 128 * 4,
                SymmetricType.DIAGONAL => Eval(b.rot0) + Eval(b.inv_rot0) - 128 * 2,
                SymmetricType.ASYMMETRIC => Eval(b.rot0) + Eval(b.inv_rot0) + Eval(b.rot90) + Eval(b.inv_rot90) +
                                                            Eval(b.rot180) + Eval(b.inv_rot180) + Eval(b.rot270) + Eval(b.inv_rot270) - 128 * 8,
                _ => throw new NotImplementedException(),
            };
        }

        public float EvalTraining(Board b)
        {
            return Weights[Hasher.Hash(b)];
        }

        public override float EvalTraining(RotatedAndMirroredBoards b)
        {
            return Type switch
            {
                SymmetricType.X_SYMMETRIC => EvalTraining(b.rot0) + EvalTraining(b.inv_rot0) + EvalTraining(b.inv_rot90) + EvalTraining(b.rot270),
                SymmetricType.XY_SYMMETRIC => EvalTraining(b.rot0) + EvalTraining(b.inv_rot0) + EvalTraining(b.inv_rot90) + EvalTraining(b.rot90),
                SymmetricType.DIAGONAL => EvalTraining(b.rot0) + EvalTraining(b.inv_rot0),
                SymmetricType.ASYMMETRIC => EvalTraining(b.rot0) + EvalTraining(b.inv_rot0) + EvalTraining(b.rot90) + EvalTraining(b.inv_rot90) +
                                                            EvalTraining(b.rot180) + EvalTraining(b.inv_rot180) + EvalTraining(b.rot270) + EvalTraining(b.inv_rot270),
                _ => throw new NotImplementedException(),
            };
        }

        public override int NumOfEvaluation(int n_discs) => Type switch
        {
            SymmetricType.X_SYMMETRIC => 4,
            SymmetricType.XY_SYMMETRIC => 4,
            SymmetricType.DIAGONAL => 2,
            SymmetricType.ASYMMETRIC => 8,
            _ => throw new NotImplementedException(),
        };

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
                WeightsB[index] = ConvertToInt8(Weights[index], WEIGHT_RANGE);
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
}
