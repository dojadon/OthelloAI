using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.Distributions;

namespace OthelloAI
{
    public abstract class Evaluator
    {
        public abstract float Eval(Board board);

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

        public override float Eval(Board board)
        {
            return Weights.Eval(new RotatedAndMirroredBoards(board));
        }

        public override float EvalTraining(Board board)
        {
            return Weights.EvalTraining(new RotatedAndMirroredBoards(board));
        }
    }

    public class EvaluatorRandomize : Evaluator
    {
        public Evaluator Evaluator { get; }
        public Random Random { get;} = new Random();
        public float V { get; }

        public EvaluatorRandomize(Evaluator evaluator, float v)
        {
            Evaluator = evaluator;
            V = v;
        }

        public override float Eval(Board board)
        {
            return Evaluator.Eval(board) + (float) Normal.Sample(Random, 0, V);
        }

        public override float EvalTraining(Board board)
        {
            return Evaluator.EvalTraining(board);
        }

        public override void StartSearch(int stone)
        {
            Evaluator.StartSearch(stone);
        }
    }

    public class EvaluatorBiasedRandomChoice : Evaluator
    {
        Random Rand { get; } = new Random();
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

        public override float Eval(Board board)
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

        public override float Eval(Board board)
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
