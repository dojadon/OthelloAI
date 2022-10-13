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

        public virtual void StartSearch(int stone)
        {
        }
    }

    public class EvaluatorPatternBased : Evaluator
    {
        public Pattern[] Patterns { get; }

        public EvaluatorPatternBased(Pattern[] patterns)
        {
            Patterns = patterns;
        }

        public override int Eval(Board board)
        {
            return EvalByPEXTHashing(board);
        }

        protected int EvalByPEXTHashing(Board board)
        {
            var boards = new RotatedAndMirroredBoards(board);
            return Patterns.Sum(p => p.EvalByPEXTHashing(boards));
        }
    }

    public class EvaluatorPatternBased_Release : Evaluator
    {
        public override int Eval(Board board)
        {
            return EvalByPEXTHashing(board);
        }

        protected int EvalByPEXTHashing(Board board)
        {
            var boards = new RotatedAndMirroredBoards(board);

            return Program.PATTERN_EDGE2X.EvalByPEXTHashing(boards)
                    + Program.PATTERN_EDGE_BLOCK.EvalByPEXTHashing(boards)
                    + Program.PATTERN_CORNER_BLOCK.EvalByPEXTHashing(boards)
                    + Program.PATTERN_CORNER.EvalByPEXTHashing(boards)
                    + Program.PATTERN_LINE1.EvalByPEXTHashing(boards)
                    + Program.PATTERN_LINE2.EvalByPEXTHashing(boards)
                    + Program.PATTERN_LINE3.EvalByPEXTHashing(boards)
                    + Program.PATTERN_DIAGONAL8.EvalByPEXTHashing(boards)
                    + Program.PATTERN_DIAGONAL7.EvalByPEXTHashing(boards);
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

        public override void StartSearch(int stone)
        {
            Current = Choice();
        }
    }

    public class EvaluatorRandomize : Evaluator
    {
        Random Rand { get; } = new Random(DateTime.Now.Millisecond);
        Evaluator Evaluator { get; }
        int Randomness { get; }

        public EvaluatorRandomize(Evaluator evaluator, int randomness)
        {
            Evaluator = evaluator;
            Randomness = randomness;
        }

        public override int Eval(Board board)
        {
            return Evaluator.Eval(board) + (Rand.Next(Randomness) - Randomness / 2);
        }
    }

}
