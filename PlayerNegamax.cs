using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace OthelloAI
{
    public abstract class PlayerAlphaBetaBased : Player
    {
        public Evaluator Evaluator { get; set; }

        public int SearchDepth { get; set; }
        public int DepthDoMoveOrdering { get; set; }
        public int ShallowSearchDepth { get; set; }

        public int StoneCountDoFullSearch { get; set; }

        public PlayerAlphaBetaBased(Evaluator evaluator)
        {
            Evaluator = evaluator;
        }

        protected float EvalFinishedGame(Board board, int stone)
        {
            return board.GetStoneCount(stone) * 10000;
        }

        protected int GetSearchDepth(Board board)
        {
            if (board.stoneCount > StoneCountDoFullSearch)
            {
                Console.WriteLine("完全読み開始");
                return 64 - board.stoneCount;
            }
            else
            {
                return SearchDepth;
            }
        }

        protected bool ShouldDoMoveOrdering(int depth)
        {
            return depth >= SearchDepth - DepthDoMoveOrdering;
        }

        protected int GetShallowSearchDepth(int depth)
        {
            return Math.Min(depth - 1, ShallowSearchDepth);
        }

        protected bool ShouldSkipSearch(int depth)
        {
            return depth <= 0; // || this.getTimeLimit(depth) > timer.timeLeft();
        }
    }

    public class PlayerNegascout : PlayerAlphaBetaBased
    {
        public int[] timeLimit;

        public PlayerNegascout(Evaluator evaluator) : base(evaluator)
        {
        }

        public long[] count = new long[5];
        public long[] time = new long[5];

        public float Eval(Board board, int stone)
        {
            if (board.stoneCount >= 60)
            {
                return board.GetStoneCount(stone);
            }
            else
            {
                return Evaluator.Eval(board, stone);
            }
        }

        public float Negascout(Board board, int stone, IEnumerable<ulong> moves, int depth, float alpha, float beta, out ulong result)
        {
            result = moves.First();
            float max = -Search(board.Reversed(result, stone), -stone, depth - 1, -beta, -alpha, out _);

            if (beta <= max)
                return max;

            alpha = Math.Max(alpha, max);

            foreach (ulong move in moves.Skip(1))
            {
                Board reversed = board.Reversed(move, stone);
                float eval = -Search(reversed, -stone, depth - 1, -alpha - 1, -alpha, out _);

                if (beta <= eval)
                    return eval;

                if (alpha < eval)
                {
                    alpha = eval;
                    eval = -Search(reversed, -stone, depth - 1, -beta, -alpha, out _);

                    if (beta <= eval)
                        return eval;

                    alpha = Math.Max(alpha, eval);
                }

                if (max < eval)
                {
                    max = eval;
                    result = move;
                }
            }
            return max;
        }

        public float Negamax(Board board, int stone, IEnumerable<ulong> moves, int depth, float alpha, float beta, out ulong result)
        {
            result = 0;

            foreach (ulong move in moves)
            {
                float eval = -Search(board.Reversed(move, stone), -stone, depth - 1, -beta, -alpha, out _);
                if (alpha < eval)
                {
                    alpha = eval;
                    result = move;
                }

                if (alpha >= beta)
                    return alpha;
            }
            return alpha;
        }

        public float Search(Board board, int stone, int depth, float alpha, float beta, out ulong result)
        {
            result = 0;

            if (ShouldSkipSearch(depth))
            {
                return Eval(board, stone);
            }

            ulong moves = board.GetMoves(stone);
            if (moves == 0)
            {
                if (board.GetMoves(-stone) == 0)
                {
                    return EvalFinishedGame(board, stone);
                }
                else
                {
                    return -Search(board, -stone, depth - 1, -beta, -alpha, out _);
                }
            }

            if (Board.BitCount(moves) == 1)
            {
                result = moves;
                return -Search(board.Reversed(moves, stone), -stone, depth - 1, -beta, -alpha, out _);
            }

            IEnumerable<ulong> movesEnumerable = new BitsEnumerable(moves);

            if (ShouldDoMoveOrdering(depth))
            {
                movesEnumerable = movesEnumerable.OrderByDescending(m =>
                {
                    return -Search(board.Reversed(m, stone), -stone, GetShallowSearchDepth(depth), -beta, -alpha, out _);
                });
                return Negascout(board, stone, movesEnumerable, depth, alpha, beta, out result);
            }
            else
            {
                return Negamax(board, stone, movesEnumerable, depth, alpha, beta, out result);
            }
        }

        public override Move DecideMove(Board board, int stone)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Search(board, stone, GetSearchDepth(board), -100000000, 10000000, out ulong result);
            sw.Stop();
            count[2]++;
            time[2] += sw.ElapsedTicks;
            return new Move(result);
        }
    }

    public class PlayerNegamax : PlayerAlphaBetaBased
    {
        public int[] timeLimit;

        public PlayerNegamax(Evaluator evaluator) : base(evaluator)
        {
        }

        public long[] count = new long[5];
        public long[] time = new long[5];

        public float Negamax(Board board, int stone, int depth, float alpha, float beta, out ulong result)
        {
            ulong dummy = 0;
            result = 0;

            if (ShouldSkipSearch(depth))
            {
                return Evaluator.Eval(board, stone);
            }

            ulong moves = board.GetMoves(stone);

            if (moves == 0)
            {
                if (board.GetMoves(-stone) == 0)
                {
                    return EvalFinishedGame(board, stone);
                }
                else
                {
                    return -Negamax(board, -stone, depth - 1, -beta, -alpha, out dummy);
                }
            }

            IEnumerable<ulong> movesEnumerable = new BitsEnumerable(moves);

            if (ShouldDoMoveOrdering(depth))
            {
                movesEnumerable = movesEnumerable.OrderByDescending(m =>
                {
                    return -Negamax(board.Reversed(m, stone), -stone, GetShallowSearchDepth(depth), -beta, -alpha, out dummy);
                });
            }

            foreach (ulong move in movesEnumerable)
            {
                float eval = -Negamax(board.Reversed(move, stone), -stone, depth - 1, -beta, -alpha, out dummy);
                if (alpha < eval)
                {
                    alpha = eval;
                    result = move;
                }

                if (alpha >= beta)
                    return alpha;
            }
            return alpha;
        }

        public override Move DecideMove(Board board, int stone)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Negamax(board, stone, GetSearchDepth(board), -100000000, 10000000, out ulong result);
            sw.Stop();
            count[2]++;
            time[2] += sw.ElapsedTicks;
            return new Move(result);
        }
    }
}
