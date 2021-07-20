using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace OthelloAI
{
    public readonly struct MoveInfo
    {
        public readonly ulong move;
        public readonly int count;
        public readonly ulong nextMoves;
        public readonly Board reversed;

        public MoveInfo(ulong move, ulong nextMoves, int count, Board reversed)
        {
            this.move = move;
            this.nextMoves = nextMoves;
            this.count = count;
            this.reversed = reversed;
        }
    }

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

        protected float EvalFinishedGame(Board board)
        {
            return board.GetStoneCountGap() * 10000;
        }

        protected float EvalFinishedGameWithLastMove(Board board)
        {
            return (board.GetStoneCountGap() + board.GetReversedCountOnLastMove() * 2 + 1) * 10000;
        }

        protected int GetSearchDepth(Board board)
        {
            if (board.stoneCount > StoneCountDoFullSearch)
            {
                Console.WriteLine("完全読み開始");
                return 64 - board.stoneCount + 10;
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

        public List<long> times = new List<long>();

        public float Eval(Board board)
        {
            return Evaluator.Eval(board);
        }

        public float Negascout(Board board, IEnumerable<ulong> moves, int depth, float alpha, float beta, out ulong result)
        {
            result = moves.First();
            float max = -Search(board.Reversed(result), depth - 1, -beta, -alpha, out _);

            if (CurrentDepth == depth)
                Console.WriteLine($"{new Move(result)} : {max}");

            if (beta <= max)
                return max;

            alpha = Math.Max(alpha, max);

            foreach (ulong move in moves.Skip(1))
            {
                Board reversed = board.Reversed(move);
                float eval = -Search(reversed, depth - 1, -alpha - 1, -alpha, out _);

                if (beta <= eval)
                {
                    if (CurrentDepth == depth)
                    {
                        Console.WriteLine($"{new Move(move)} : Rejected");
                    }
                    return eval;
                }

                if (alpha < eval)
                {
                    alpha = eval;
                    eval = -Search(reversed, depth - 1, -beta, -alpha, out _);

                    if (CurrentDepth == depth)
                    {
                        Console.WriteLine($"{new Move(move)} : {eval}");
                    }

                    if (beta <= eval)
                        return eval;

                    alpha = Math.Max(alpha, eval);
                }
                else
                {
                    if (CurrentDepth == depth)
                        Console.WriteLine($"{new Move(move)} : Rejected");
                }

                if (max < eval)
                {
                    max = eval;
                    result = move;
                }
            }
            return max;
        }

        public float Negamax(Board board, IEnumerable<ulong> moves, int depth, float alpha, float beta, out ulong result)
        {
            result = 0;

            foreach (ulong move in moves)
            {
                float eval = -Search(board.Reversed(move), depth - 1, -beta, -alpha, out _);
                if (alpha < eval)
                {
                    alpha = eval;
                    result = move;
                }

                if (CurrentDepth == depth)
                    Console.WriteLine($"{new Move(move)} : {eval}");

                if (alpha >= beta)
                    return alpha;
            }
            return alpha;
        }

        public float Search(Board board, int depth, float alpha, float beta, out ulong result)
        {
            result = 0;

            if (ShouldSkipSearch(depth))
            {
                return Eval(board);
            }

            ulong moves = board.GetMoves();
            if (moves == 0)
            {
                if (board.GetOpponentMoves() == 0)
                {
                    return EvalFinishedGame(board);
                }
                else
                {
                    return -Search(board.ColorFliped(), depth - 1, -beta, -alpha, out _);
                }
            }

            int count = Board.BitCount(moves);
            if (count == 1)
            {
                result = moves;

                if (board.stoneCount == 63)
                {
                    return -EvalFinishedGame(board.Reversed(moves));
                }
                else
                {
                    return -Search(board.Reversed(moves), depth - 1, -beta, -alpha, out _);
                }
            }

            IEnumerable<ulong> movesEnumerable = new BitsEnumerable(moves);

            if (count > 3 && ShouldDoMoveOrdering(depth))
            {
                movesEnumerable = movesEnumerable.OrderBy(m =>
                {
                    return Board.BitCount(board.Reversed(m).GetMoves());
                });
                return Negascout(board, movesEnumerable, depth, alpha, beta, out result);
            }
            else
            {
                return Negamax(board, movesEnumerable, depth, alpha, beta, out result);
            }
        }

        int CurrentDepth { get; set; }

        public override Move DecideMove(Board board, int stone)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            CurrentDepth = GetSearchDepth(board);

            if (stone == -1)
                board = board.ColorFliped();

            Console.WriteLine(Search(board, GetSearchDepth(board), -100000000, 10000000, out ulong result));
            sw.Stop();

            times.Add(sw.ElapsedTicks);

            return new Move(result);
        }
    }
}
