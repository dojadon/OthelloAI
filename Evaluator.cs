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
            return EvalByPEXTHashing(board);
        }

        protected int EvalByPEXTHashing(Board board)
        {
            var boards = new Boards(board);

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
