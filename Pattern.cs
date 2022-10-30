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
        public static Pattern Create(BoardHasher hasher, int n_stages, PatternType type, string file = "", bool load = false)
        {
            PatternWeights[] weights_stage = Enumerable.Range(0, n_stages).Select(_ => new PatternWeightsArray(hasher)).ToArray();
            var weights = new PatternWeightsStagebased(weights_stage);
            var pattern = new Pattern(weights, type) { FilePath = file };

            if (load)
                pattern.Load();

            return pattern;
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
            // float[] w = Weights.GetWeights();
            // float range = w.Select(Math.Abs).OrderByDescending(f => f).Take(Math.Max(w.Length / 4, w.Length / 100)).Average();
            float range = 10;

            Weights.ApplyTrainedEvaluation(range);
        }

        public int Eval(RotatedAndMirroredBoards b)
        {
            int _Eval(in Board board) => Weights.Eval(board);

            return Type switch
            {
                PatternType.X_SYMMETRIC => _Eval(b.rot0) + _Eval(b.inv_rot0) + _Eval(b.inv_rot90) + _Eval(b.rot270) - 128 * 4,
                PatternType.XY_SYMMETRIC => _Eval(b.rot0) + _Eval(b.inv_rot0) + _Eval(b.inv_rot90) + _Eval(b.rot90) - 128 * 4,
                PatternType.DIAGONAL => _Eval(b.rot0) + _Eval(b.inv_rot0) - 128 * 2,
                PatternType.ASYMMETRIC => _Eval(b.rot0) + _Eval(b.inv_rot0) + _Eval(b.rot90) + _Eval(b.inv_rot90) + 
                                                            _Eval(b.rot180) + _Eval(b.inv_rot180) + _Eval(b.rot270) + _Eval(b.inv_rot270) - 128 * 8,
                _ => throw new NotImplementedException(),
            };
        }

        public float EvalTraining(RotatedAndMirroredBoards b)
        {
            float _Eval(in Board board) => Weights.EvalTraining(board);

            return Type switch
            {
                PatternType.X_SYMMETRIC => _Eval(b.rot0) + _Eval(b.inv_rot0) + _Eval(b.inv_rot90) + _Eval(b.rot270),
                PatternType.XY_SYMMETRIC => _Eval(b.rot0) + _Eval(b.inv_rot0) + _Eval(b.inv_rot90) + _Eval(b.rot90),
                PatternType.DIAGONAL => _Eval(b.rot0) + _Eval(b.inv_rot0),
                PatternType.ASYMMETRIC => _Eval(b.rot0) + _Eval(b.inv_rot0) + _Eval(b.rot90) + _Eval(b.inv_rot90) +
                                                            _Eval(b.rot180) + _Eval(b.inv_rot180) + _Eval(b.rot270) + _Eval(b.inv_rot270),
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
