using System;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;

namespace OthelloAI
{
    public abstract class Weight
    {
        public const float WEIGHT_RANGE = 10;

        public static Weight Create(BoardHasher hasher, int n_stages)
        {
            Weight[] weights_stage = Enumerable.Range(0, n_stages).Select(_ => new WeightsArray(hasher)).ToArray();
            return new WeightsStagebased(weights_stage);
        }

        public abstract void Reset();
        public abstract int Eval(Board b);
        public abstract float EvalTraining(Board b);
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
        Weight[] Weights { get; }

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

        public override int Eval(Board b)
        {
            return Weights.Sum(w => w.Eval(b));
        }

        public override float EvalTraining(Board b)
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

        public override int Eval(Board b)
        {
            return GetCurrentWeights(b).Eval(b);
        }

        public override float EvalTraining(Board b)
        {
            return GetCurrentWeights(b).EvalTraining(b);
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

    public class WeightsArrayS : Weight
    {
        public BoardHasher Hasher { get; }

        public float[] weights;
        byte[] weights_b;

        readonly int[][] pos = new int[8][];

        public override float[] GetWeights() => weights;

        public int NumOfStates { get; }

        public WeightsArrayS(ulong m)
        {
            NumOfStates = Hasher.NumOfStates;

            var boards = new RotatedAndMirroredBoards(new Board(m, 0));
            foreach((var b, var i) in boards.Select((b, i) => (b, i)))
            {
                pos[i] = BoardHasherMask.MaskToPositions(b.bitB);
            }

            Hasher = new BoardHasherScanning(pos[0]);

            Reset();
        }

        public override void Reset()
        {
            weights = new float[Hasher.ArrayLength];
            weights_b = new byte[Hasher.ArrayLength];
        }

        public int Hash(Board board, int idx)
        {
            int hash = 0;

            for (int i = 0; i < pos[idx].Length; i++)
            {
                int p = pos[idx][i];

                hash *= 3;
                hash += (int)((board.bitB >> p) & 1);
                hash += (int)((board.bitW >> p) & 1) * 2;
            }
            return hash;
        }

        public int Eval(Board b, int i)
        {
            return weights_b[Hash(b, i)];
        }

        public override int Eval(Board b)
        {
            return Eval(b, 0) + Eval(b, 1) + Eval(b, 2) + Eval(b, 3)
                + Eval(b, 4) + Eval(b, 5) + Eval(b, 6) + Eval(b, 7) - 128 * 8;
        }

        public float EvalTraining_(Board b, int i)
        {
            return weights[Hash(b, i)];
        }

        public override float EvalTraining(Board b)
        {
            return EvalTraining_(b, 0) + EvalTraining_(b, 1) + EvalTraining_(b, 2) + EvalTraining_(b, 3)
                + EvalTraining_(b, 4) + EvalTraining_(b, 5) + EvalTraining_(b, 6) + EvalTraining_(b, 7);
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

        public  float[] weights;
        byte[] weights_b;

        readonly ulong mask;
        readonly int hash_length;

        public override float[] GetWeights() => weights;

        public int NumOfStates { get; }

        public WeightsArrayR(ulong m)
        {
            mask = m;
            hash_length = Board.BitCount(mask);

            Hasher = new BoardHasherMask(mask);
            NumOfStates = Hasher.NumOfStates;

            Reset();
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

        public override int Eval(Board board)
        {
            var b = new RotatedAndMirroredBoards(board);
            return Eval_(b.rot0) + Eval_(b.inv_rot0) + Eval_(b.rot90) + Eval_(b.inv_rot90)
                + Eval_(b.rot180) + Eval_(b.inv_rot180) + Eval_(b.rot270) + Eval_(b.inv_rot270) - 128 * 8;
        }

        public float EvalTraining__(Board b)
        {
            return weights[Hasher.Hash(b)];
        }

        public override float EvalTraining(Board board)
        {
            var b = new RotatedAndMirroredBoards(board);
            return EvalTraining__(b.rot0) + EvalTraining__(b.inv_rot0) + EvalTraining__(b.rot90) + EvalTraining__(b.inv_rot90) +
                EvalTraining__(b.rot180) + EvalTraining__(b.inv_rot180) + EvalTraining__(b.rot270) + EvalTraining__(b.inv_rot270);
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

    public class WeightsArray : Weight
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

        public int Eval_(Board b)
        {
            return WeightsB[Hasher.Hash(b)];
        }

        public override int Eval(Board board)
        {
            var b = new RotatedAndMirroredBoards(board);

            return Type switch
            {
                SymmetricType.X_SYMMETRIC => Eval_(b.rot0) + Eval_(b.inv_rot0) + Eval_(b.inv_rot90) + Eval_(b.rot270) - 128 * 4,
                SymmetricType.XY_SYMMETRIC => Eval_(b.rot0) + Eval_(b.inv_rot0) + Eval_(b.inv_rot90) + Eval_(b.rot90) - 128 * 4,
                SymmetricType.DIAGONAL => Eval_(b.rot0) + Eval_(b.inv_rot0) - 128 * 2,
                SymmetricType.ASYMMETRIC => Eval_(b.rot0) + Eval_(b.inv_rot0) + Eval_(b.rot90) + Eval_(b.inv_rot90) +
                                                            Eval_(b.rot180) + Eval_(b.inv_rot180) + Eval_(b.rot270) + Eval_(b.inv_rot270) - 128 * 8,
                _ => throw new NotImplementedException(),
            };
        }

        public float EvalTraining__(Board b)
        {
            return Weights[Hasher.Hash(b)];
        }

        public override float EvalTraining(Board board)
        {
            var b = new RotatedAndMirroredBoards(board);

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
