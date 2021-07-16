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
        public abstract float Eval(Board board, int stone);
    }

    public class EvaluatorPatternBased : Evaluator
    {
        public override float Eval(Board board, int stone)
        {
            /*
            float eval = 0;
            for (int i = 0; i < Patterns.Length; i++)
            {
                eval += Patterns[i].Eval(board, stone) * Weight[i];
            }
            return eval;
            */

            MirroredNeededBoards.Create(board, out Board b1, out Board b2, out Board b3, out Board b4);

            return Program.PATTERN_EDGE2X.Eval(board, b1, b2, b3, b4, stone) * 2.4F
                + Program.PATTERN_EDGE_BLOCK.Eval(board, b1, b2, b3, b4, stone) * 1.2F
                + Program.PATTERN_CORNER_BLOCK.Eval(board, b1, b2, b3, b4, stone) * 1.8F
                + Program.PATTERN_CORNER.Eval(board, b1, b2, b3, b4, stone) * 1.5F
                + Program.PATTERN_LINE1.Eval(board, b1, b2, b3, b4, stone) * 1.2F
                + Program.PATTERN_LINE2.Eval(board, b1, b2, b3, b4, stone)
                + Program.PATTERN_LINE3.Eval(board, b1, b2, b3, b4, stone)
                + Program.PATTERN_DIAGONAL8.Eval(board, b1, b2, b3, b4, stone)
                + Program.PATTERN_DIAGONAL7.Eval(board, b1, b2, b3, b4, stone);
        }
    }

    public class EvaluatorRandomize : Evaluator
    {
        Random Rand { get; } = new Random(DateTime.Now.Millisecond);
        Evaluator Evaluator { get; }
        float Randomness { get; }

        public EvaluatorRandomize(Evaluator evaluator, float randomness)
        {
            Evaluator = evaluator;
            Randomness = randomness;
        }

        public override float Eval(Board board, int stone)
        {
            return Evaluator.Eval(board, stone) + (float)Rand.NextDouble() * Randomness;
        }
    }

}
