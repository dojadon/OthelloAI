using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OthelloAI
{
    public abstract class Evaluator
    {
        public abstract int Eval(Board board);

        public abstract float EvalTraining(Board board);

        public virtual void StartSearch(int stone)
        {
        }
    }

    public class EvaluatorWeightsBased : Evaluator
    {
        public Weights Weights { get; }

        public EvaluatorWeightsBased(Weights weights)
        {
            Weights = weights;
        }

        public override int Eval(Board board)
        {
            var boards = new RotatedAndMirroredBoards(board);
            return Weights.Eval(boards);
        }

        public override float EvalTraining(Board board)
        {
            var boards = new RotatedAndMirroredBoards(board);
            return Weights.EvalTraining(boards);
        }
    }

    public class EvaluatorPatternBased_Release : Evaluator
    {
        public override int Eval(Board board)
        {
            var boards = new RotatedAndMirroredBoards(board);

            return Program.PATTERN_EDGE2X.Eval(boards)
                    + Program.PATTERN_EDGE_BLOCK.Eval(boards)
                    + Program.PATTERN_CORNER_BLOCK.Eval(boards)
                    + Program.PATTERN_CORNER.Eval(boards)
                    + Program.PATTERN_LINE1.Eval(boards)
                    + Program.PATTERN_LINE2.Eval(boards);
        }

        public override float EvalTraining(Board board)
        {
            var boards = new RotatedAndMirroredBoards(board);

            return Program.PATTERN_EDGE2X.EvalTraining(boards)
                    + Program.PATTERN_EDGE_BLOCK.EvalTraining(boards)
                    + Program.PATTERN_CORNER_BLOCK.EvalTraining(boards)
                    + Program.PATTERN_CORNER.EvalTraining(boards)
                    + Program.PATTERN_LINE1.EvalTraining(boards)
                    + Program.PATTERN_LINE2.EvalTraining(boards);
        }
    }

    public class EvaluatorBiasedRandomChoice : Evaluator
    {
        Random Rand { get; } = new Random(DateTime.Now.Millisecond);
        (Evaluator e, float prob)[] Evaluators { get; }

        Evaluator Current { get; set; }

        public EvaluatorBiasedRandomChoice((Evaluator, float)[] evaluators)
        {
            Evaluators = evaluators;
        }

        Evaluator Choice()
        {
            double d = Rand.NextDouble();

            float total = 0;
            foreach((Evaluator e, float prob) in Evaluators)
            {
                if (d < (total += prob))
                    return e;
            }

            return Evaluators[^1].e;
        }

        public override int Eval(Board board)
        {
            return Current.Eval(board);
        }

        public override float EvalTraining(Board board)
        {
            return Current.Eval(board);
        }

        public override void StartSearch(int stone)
        {
            Current = Choice();
        }
    }

    public class EvaluatorRandomChoice : Evaluator
    {
        Random Rand { get; } = new Random(DateTime.Now.Millisecond);
        Evaluator[] Evaluators { get; }

        Evaluator Current { get; set; }

        public EvaluatorRandomChoice(Evaluator[] evaluators)
        {
            Evaluators = evaluators;
        }

        Evaluator Choice()
        {
            return Evaluators[Rand.Next(Evaluators.Length)];
        }

        public override int Eval(Board board)
        {
            return Current.Eval(board);
        }

        public override float EvalTraining(Board board)
        {
            return Current.Eval(board);
        }

        public override void StartSearch(int stone)
        {
            Current = Choice();
        }
    }
}
