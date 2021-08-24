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
            MirroredNeededBoards.Create(board, out Board b1, out Board b2, out Board b3, out Board b4);

            return Program.PATTERN_EDGE2X.Eval(board, b1, b2, b3, b4)
                    + Program.PATTERN_EDGE_BLOCK.Eval(board, b1, b2, b3, b4)
                    + Program.PATTERN_CORNER_BLOCK.Eval(board, b1, b2, b3, b4)
                    + Program.PATTERN_CORNER.Eval(board, b1, b2, b3, b4)
                    + Program.PATTERN_LINE1.Eval(board, b1, b2, b3, b4)
                    + Program.PATTERN_LINE2.Eval(board, b1, b2, b3, b4)
                    + Program.PATTERN_LINE3.Eval(board, b1, b2, b3, b4)
                    + Program.PATTERN_DIAGONAL8.Eval(board, b1, b2, b3, b4)
                    + Program.PATTERN_DIAGONAL7.Eval(board, b1, b2, b3, b4);
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
