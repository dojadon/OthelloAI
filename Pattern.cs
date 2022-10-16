using System;
using System.IO;
using System.Linq;

namespace OthelloAI
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
        public static Pattern Create(BoardHasher hasher, int n_stages, PatternType type, string file = "")
        {
            PatternWeights[] weights_stage;

            if (hasher.Positions.Length > 1)
            {
                weights_stage = Enumerable.Range(0, n_stages).Select(_ => new PatternWeightsArray(hasher)).ToArray();
            }
            else
            {
                ulong mask1 = 1UL << hasher.Positions[0];
                ulong mask2 = 1UL << hasher.Positions[1];

                weights_stage = Enumerable.Range(0, n_stages).Select(_ => new PatternWeights2Disc(mask1, mask2)).ToArray();
            }

            var weights = new PatternWeightsStagebased(weights_stage);
            return new Pattern(weights, type) { FilePath = file };
        }

        public string FilePath { get; set; }

        public PatternType Type { get; }

        public PatternWeights Weights { get; private set; }

        public Pattern(PatternWeights weights, PatternType type)
        {
            Weights = weights;
            Type = type;

            Reset();
        }

        public void Reset()
        {
            Weights.Reset();
        }

        public void UpdataEvaluation(Board board, float add, float range)
        {
            Weights.UpdataEvaluation(board, add, range);
        }

        public void ApplyTrainedEvaluation()
        {
            float[] w = Weights.GetWeights();
            float range = w.Select(Math.Abs).OrderByDescending(f => f).Take(Math.Max(w.Length / 4, w.Length / 100)).Average();

            Weights.ApplyTrainedEvaluation(range);
        }

        public int EvalByPEXTHashing(RotatedAndMirroredBoards b)
        {
            int _Eval(in Board board) => Weights.Eval(board);

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
            float _Eval(in Board board) => Weights.EvalTraining(board);

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
            Weights.Read(reader);
            ApplyTrainedEvaluation();
        }

        public void Load()
        {
            using var reader = new BinaryReader(new FileStream(FilePath, FileMode.Open));
            Read(reader);
        }

        public void Write(BinaryWriter writer)
        {
            Weights.Write(writer);
        }

        public void Save()
        {
            using var writer = new BinaryWriter(new FileStream(FilePath, FileMode.Create));
            Write(writer);
        }
    }
}
