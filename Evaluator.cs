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
        public Weight Weights { get; }

        public EvaluatorWeightsBased(Weight weights)
        {
            Weights = weights;
        }

        public override int Eval(Board board)
        {
            return Weights.Eval(new RotatedAndMirroredBoards(board));
        }

        public override float EvalTraining(Board board)
        {
            return Weights.EvalTraining(new RotatedAndMirroredBoards(board));
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
