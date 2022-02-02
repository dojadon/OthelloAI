using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OthelloAI.Patterns;

namespace OthelloAI
{
    public abstract class Evaluator
    {
        public abstract int Eval(Board board);
    }

    public class EvaluatorPatternBased : Evaluator
    {
        public override int Eval(Board board)
        {
            return Program.PATTERN_EDGE2X.Eval(board)
                    + Program.PATTERN_EDGE_BLOCK.Eval(board)
                    + Program.PATTERN_CORNER_BLOCK.Eval(board)
                    + Program.PATTERN_CORNER.Eval(board)
                    + Program.PATTERN_LINE1.Eval(board)
                    + Program.PATTERN_LINE2.Eval(board)
                    + Program.PATTERN_LINE3.Eval(board)
                    + Program.PATTERN_DIAGONAL8.Eval(board)
                    + Program.PATTERN_DIAGONAL7.Eval(board);
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
