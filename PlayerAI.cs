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
        public int StoneCountDoFullSearch { get; set; }

        public PlayerAlphaBetaBased(Evaluator evaluator)
        {
            Evaluator = evaluator;
        }

        protected float EvalFinishedGame(Board board)
        {
            return board.GetStoneCountGap() * 10000;
        }

        protected int GetSearchDepth(Board board)
        {
            if (board.stoneCount > StoneCountDoFullSearch)
            {
                return 64 - board.stoneCount + 1;
            }
            else
            {
                return SearchDepth;
            }
        }
    }

    public class PlayerNegascout : PlayerAlphaBetaBased
    {
        public PlayerNegascout(Evaluator evaluator) : base(evaluator)
        {
        }

        public List<float> times = new List<float>();

        public float Eval(Board board)
        {
            return Evaluator.Eval(board);
        }

        public ulong NegascoutRootDefinite(Board board, int depth)
        {
            ulong moves = board.GetMoves();

            if (Board.BitCount(moves) <= 1)
            {
                return moves;
            }

            IEnumerable<ulong> movesEnumerable = new BitsEnumerable(moves).OrderBy(m =>
            {
                return Board.BitCount(board.Reversed(m).GetMoves());
            });

            ulong result = movesEnumerable.First();
            float max = -SearchDefinite(board.Reversed(result), depth - 1, -1000000, 1000000);
            float alpha = max;

            Console.WriteLine($"{Board.ToPos(result)} : {max}");

            if (0 < max)
                return result;

            foreach (ulong move in movesEnumerable.Skip(1))
            {
                Board reversed = board.Reversed(move);
                float eval = -SearchDefinite(reversed, depth - 1, -alpha - 1, -alpha);

                if (0 < eval)
                    return move;

                if (alpha < eval)
                {
                    alpha = eval;
                    eval = -SearchDefinite(reversed, depth - 1, -1000000, -alpha);

                    if (0 < eval)
                        return move;

                    alpha = Math.Max(alpha, eval);

                    Console.WriteLine($"{Board.ToPos(move)} : {eval}");
                }
                else
                {
                    Console.WriteLine($"{Board.ToPos(move)} : Rejected");
                }

                if (max < eval)
                {
                    max = eval;
                    result = move;
                }
            }
            return result;
        }

        public float NegascoutDefinite(Board board, IEnumerable<ulong> moves, int depth, float alpha, float beta)
        {
            float max = -SearchDefinite(board.Reversed(moves.First()), depth - 1, -beta, -alpha);

            if (beta <= max || 0 < max)
                return max;

            alpha = Math.Max(alpha, max);

            foreach (ulong move in moves.Skip(1))
            {
                Board reversed = board.Reversed(move);
                float eval = -SearchDefinite(reversed, depth - 1, -alpha - 1, -alpha);

                if (beta <= eval)
                    return eval;

                if (alpha < eval)
                {
                    alpha = eval;
                    eval = -SearchDefinite(reversed, depth - 1, -beta, -alpha);

                    if (beta <= eval || 0 < eval)
                        return eval;

                    alpha = Math.Max(alpha, eval);
                }
                max = Math.Max(max, eval);
            }
            return max;
        }

        public float NegamaxDefinite(Board board, IEnumerable<ulong> moves, int depth, float alpha, float beta)
        {
            foreach (ulong move in moves)
            {
                alpha = Math.Max(alpha, -SearchDefinite(board.Reversed(move), depth - 1, -beta, -alpha));

                if (alpha >= beta)
                    return alpha;
            }
            return alpha;
        }

        public float SearchDefinite(Board board, int depth, float alpha, float beta)
        {
            ulong moves = board.GetMoves();
            if (moves == 0)
            {
                if (board.GetOpponentMoves() == 0)
                {
                    return EvalFinishedGame(board);
                }
                else
                {
                    return -SearchDefinite(board.ColorFliped(), depth - 1, -beta, -alpha);
                }
            }

            int count = Board.BitCount(moves);
            if (count == 1)
            {
                if (board.stoneCount == 63)
                {
                    return -EvalFinishedGame(board.Reversed(moves));
                }
                else
                {
                    return -SearchDefinite(board.Reversed(moves), depth - 1, -beta, -alpha);
                }
            }

            IEnumerable<ulong> movesEnumerable = new BitsEnumerable(moves);

            if (count > 3 && depth >= 3)
            {
                movesEnumerable = movesEnumerable.OrderBy(m =>
                {
                    return Board.BitCount(board.Reversed(m).GetMoves());
                });
                return NegascoutDefinite(board, movesEnumerable, depth, alpha, beta);
            }
            else
            {
                return NegamaxDefinite(board, movesEnumerable, depth, alpha, beta);
            }
        }

        public ulong NegascoutRoot(Board board, int depth)
        {
            ulong moves = board.GetMoves();

            if (Board.BitCount(moves) <= 1)
            {
                return moves;
            }

            IEnumerable<ulong> movesEnumerable = new BitsEnumerable(moves).OrderBy(m =>
            {
                return Board.BitCount(board.Reversed(m).GetMoves());
            });

            ulong result = movesEnumerable.First();
            float max = -Search(board.Reversed(result), depth - 1, -1000000, 1000000);
            float alpha = max;

            Console.WriteLine($"{Board.ToPos(result)} : {max}");

            foreach (ulong move in movesEnumerable.Skip(1))
            {
                Board reversed = board.Reversed(move);
                float eval = -Search(reversed, depth - 1, -alpha - 1, -alpha);

                if (alpha < eval)
                {
                    alpha = eval;
                    eval = -Search(reversed, depth - 1, -1000000, -alpha);
                    alpha = Math.Max(alpha, eval);

                    Console.WriteLine($"{Board.ToPos(move)} : {eval}");
                }
                else
                {
                    Console.WriteLine($"{Board.ToPos(move)} : Rejected");
                }

                if (max < eval)
                {
                    max = eval;
                    result = move;
                }
            }
            return result;
        }

        public float Negascout(Board board, IEnumerable<ulong> moves, int depth, float alpha, float beta)
        {
            float max = -Search(board.Reversed(moves.First()), depth - 1, -beta, -alpha);

            if (beta <= max)
                return max;

            alpha = Math.Max(alpha, max);

            foreach (ulong move in moves.Skip(1))
            {
                Board reversed = board.Reversed(move);
                float eval = -Search(reversed, depth - 1, -alpha - 1, -alpha);

                if (beta <= eval)
                    return eval;

                if (alpha < eval)
                {
                    alpha = eval;
                    eval = -Search(reversed, depth - 1, -beta, -alpha);

                    if (beta <= eval)
                        return eval;

                    alpha = Math.Max(alpha, eval);
                }
                max = Math.Max(max, eval);
            }
            return max;
        }

        public float Negamax(Board board, IEnumerable<ulong> moves, int depth, float alpha, float beta)
        {
            foreach (ulong move in moves)
            {
                alpha = Math.Max(alpha, -Search(board.Reversed(move), depth - 1, -beta, -alpha));

                if (alpha >= beta)
                    return alpha;
            }
            return alpha;
        }

        public float Search(Board board, int depth, float alpha, float beta)
        {
            if (depth <= 0)
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
                    return -Search(board.ColorFliped(), depth - 1, -beta, -alpha);
                }
            }

            int count = Board.BitCount(moves);
            if (count == 1)
            {
                if (board.stoneCount == 63)
                {
                    return -EvalFinishedGame(board.Reversed(moves));
                }
                else
                {
                    return -Search(board.Reversed(moves), depth - 1, -beta, -alpha);
                }
            }

            IEnumerable<ulong> movesEnumerable = new BitsEnumerable(moves);

            if (count > 3 && depth >= 3)
            {
                movesEnumerable = movesEnumerable.OrderBy(m =>
                {
                    return Board.BitCount(board.Reversed(m).GetMoves());
                });
                return Negascout(board, movesEnumerable, depth, alpha, beta);
            }
            else
            {
                return Negamax(board, movesEnumerable, depth, alpha, beta);
            }
        }

        public override (int x, int y, ulong move) DecideMove(Board board, int stone)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            if (stone == -1)
                board = board.ColorFliped();

            ulong result;
            if (board.stoneCount > StoneCountDoFullSearch)
            {
                Console.WriteLine("必勝読み開始");
                result = NegascoutRootDefinite(board, GetSearchDepth(board));
            }
            else
            {
                result = NegascoutRoot(board, GetSearchDepth(board));
            }
            sw.Stop();

            float time = 1000F * sw.ElapsedTicks / System.Diagnostics.Stopwatch.Frequency;
            times.Add(time);
            Console.WriteLine($"Taken Time : {time} ms");

            (int x, int y) = Board.ToPos(result);

            return (x, y, result);
        }
    }
}
