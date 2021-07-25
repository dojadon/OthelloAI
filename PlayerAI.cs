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

        protected float EvalLastMove(Board board)
        {
            return (board.GetStoneCountGap() + board.GetReversedCountOnLastMove()) * 10000;
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
            Move root = new Move(board);

            if (root.count <= 1)
            {
                return root.moves;
            }

            Move[] array = CreateMoveArray(root.reversed, root.moves, root.count);

            Array.Sort(array);

            Move result = array[0];
            float max = -SearchDefinite(result, depth - 1, -1000000, 1000000);
            float alpha = max;

            Console.WriteLine($"{Board.ToPos(result.move)} : {max}");

            if (0 < max)
                return result.move;

            for (int i = 1; i < array.Length; i++)
            {
                Move move = array[i];
                float eval = -SearchDefinite(move, depth - 1, -alpha - 1, -alpha);

                if (0 < eval)
                    return move.move;

                if (alpha < eval)
                {
                    alpha = eval;
                    eval = -SearchDefinite(move, depth - 1, -1000000, -alpha);

                    if (0 < eval)
                        return move.move;

                    alpha = Math.Max(alpha, eval);

                    Console.WriteLine($"{Board.ToPos(move.move)} : {eval}");
                }
                else
                {
                    Console.WriteLine($"{Board.ToPos(move.move)} : Rejected");
                }

                if (max < eval)
                {
                    max = eval;
                    result = move;
                }
            }
            return result.move;
        }

        public float NegascoutDefinite(Move[] moves, int depth, float alpha, float beta)
        {
            float max = -SearchDefinite(moves[0], depth - 1, -beta, -alpha);

            if (beta <= max || 0 < max)
                return max;

            alpha = Math.Max(alpha, max);

            for (int i = 0; i < moves.Length; i++)
            {
                Move move = moves[i];
                float eval = -SearchDefinite(move, depth - 1, -alpha - 1, -alpha);

                if (beta <= eval || 0 < max)
                    return eval;

                max = Math.Max(max, eval);
               // alpha = Math.Max(alpha, max);
            }
            return max;
        }

        public float SearchDefinite(Move move, int depth, float alpha, float beta)
        {
            SearchedNodeCount++;

            if (move.moves == 0)
            {
                ulong opponentMoves = move.reversed.GetOpponentMoves();
                if (opponentMoves == 0)
                {
                    return EvalFinishedGame(move.reversed);
                }
                else
                {
                    return SearchDefinite(new Move(move.reversed.ColorFliped(), 0, opponentMoves, Board.BitCount(opponentMoves)), depth - 1, -beta, -alpha);
                }
            }
            
            if (move.count == 1)
            {
                if (move.reversed.stoneCount == 63)
                {
                    return -EvalLastMove(move.reversed);
                }
                else
                {
                    return -SearchDefinite(new Move(move.reversed, move.moves), depth - 1, -beta, -alpha);
                }
            }

            if (move.count == 2)
            {
                ulong next1 = Board.NextMove(move.moves);
                ulong next2 = Board.RemoveMove(move.moves, next1);

                alpha = Math.Max(alpha, -SearchDefinite(new Move(move.reversed, next1), depth - 1, -beta, -alpha));

                if (alpha >= beta)
                    return alpha;

                return Math.Max(alpha, -SearchDefinite(new Move(move.reversed, next2), depth - 1, -beta, -alpha));
            }

            Move[] array = CreateMoveArray(move.reversed, move.moves, move.count);

            float lower = -1000000;
            float upper = 1000000;

            if (BorderTable.ContainsKey(move.reversed))
            {
                CollidedCount++;

                (lower, upper) = BorderTable[move.reversed];

                if (lower >= beta)
                    return lower;

                if (upper <= alpha || upper == lower)
                    return upper;

                alpha = Math.Max(alpha, lower);
                beta = Math.Min(beta, upper);
            }

            float value;
            if (depth >= 2)
            {
                Array.Sort(array);
                value = NegascoutDefinite(array, depth, alpha, beta);
            }
            else
            {
                for (int i = 0; i < array.Length; i++)
                {
                    alpha = Math.Max(alpha, -SearchDefinite(array[i], depth - 1, -beta, -alpha));

                    if (alpha >= beta || alpha > 0)
                        break;
                }
                value =  alpha;
            }

            {
                if (value <= alpha)
                    BorderTable[move.reversed] = (lower, value);
                else if (value >= beta)
                    BorderTable[move.reversed] = (value, upper);
                else
                    BorderTable[move.reversed] = (value, value);
            }

            return value;
        }

        public ulong NegascoutRoot(Board board, int depth)
        {
            Move root = new Move(board);

            if (root.count <= 1)
            {
                return root.moves;
            }

            Move[] array = CreateMoveArray(root.reversed, root.moves, root.count);

            Array.Sort(array);

            Move result = array[0];
            float max = -Search(array[0], depth - 1, -1000000, 1000000);
            float alpha = max;

            Console.WriteLine($"{Board.ToPos(result.move)} : {max}");

            for (int i = 1; i < array.Length; i++)
            {
                Move move = array[i];

                float eval = -Search(move, depth - 1, -alpha - 1, -alpha);

                if (alpha < eval)
                {
                    alpha = eval;
                    eval = -Search(move, depth - 1, -1000000, -alpha);
                    alpha = Math.Max(alpha, eval);

                    Console.WriteLine($"{Board.ToPos(move.move)} : {eval}");
                }
                else
                {
                    Console.WriteLine($"{Board.ToPos(move.move)} : Rejected");
                }

                if (max < eval)
                {
                    max = eval;
                    result = move;
                }
            }
            return result.move;
        }

        Dictionary<Board, (float, float)> BorderTable { get; set; }

        public float Negascout(Move[] moves, int depth, float alpha, float beta)
        {
            float max = -Search(moves[0], depth - 1, -beta, -alpha);

            if (beta <= max)
                return max;

            alpha = Math.Max(alpha, max);

            for (int i = 1; i < moves.Length; i++)
            {
                Move move = moves[i];
                float eval = -Search(move, depth - 1, -alpha - 1, -alpha);

                if (beta <= eval)
                    return eval;

                if (alpha < eval)
                {
                    alpha = eval;
                    eval = -Search(move, depth - 1, -beta, -alpha);

                    if (beta <= eval)
                        return eval;

                    alpha = Math.Max(alpha, eval);
                }
                max = Math.Max(max, eval);
            }
            return max;
        }

        public float Negamax(Move[] moves, int depth, float alpha, float beta)
        {
            for (int i = 0; i < moves.Length; i++)
            {
                alpha = Math.Max(alpha, -Search(moves[i], depth - 1, -beta, -alpha));

                if (alpha >= beta)
                    return alpha;
            }
            return alpha;
        }

        public float Search(Move move, int depth, float alpha, float beta)
        {
            SearchedNodeCount++;

            if (depth <= 0)
            {
                return Eval(move.reversed);
            }

            if (move.moves == 0)
            {
                ulong opponentMoves = move.reversed.GetOpponentMoves();
                if (opponentMoves == 0)
                {
                    return EvalFinishedGame(move.reversed);
                }
                else
                {
                    return -Search(new Move(move.reversed.ColorFliped(), 0, opponentMoves, Board.BitCount(opponentMoves)), depth - 1, -beta, -alpha);
                }
            }

            if (move.count == 1)
            {
                /*  if (move.reversed.stoneCount == 62)
                  {
                      return EvalLastMove(move.reversed.Reversed(move.moves));
                  }
                  else*/
                if (move.reversed.stoneCount == 63)
                {
                    return -EvalFinishedGame(move.reversed.Reversed(move.moves));
                    return -EvalLastMove(move.reversed);
                }
                else
                {
                    return -Search(new Move(move.reversed, move.moves), depth - 1, -beta, -alpha);
                }
            }

            float lower = -1000000;
            float upper = 1000000;

            if (BorderTable.ContainsKey(move.reversed))
            {
                CollidedCount++;

                (lower, upper) = BorderTable[move.reversed];

                if (lower >= beta)
                    return lower;

                if (upper <= alpha || upper == lower)
                    return upper;

                alpha = Math.Max(alpha, lower);
                beta = Math.Min(beta, upper);
            }

            Move[] array = CreateMoveArray(move.reversed, move.moves, move.count);

            float value;
            if (move.count > 3 && depth >= 3)
            {
                Array.Sort(array);
                value = Negascout(array, depth, alpha, beta);
            }
            else
            {
                value = Negamax(array, depth, alpha, beta);
            }

            if (value <= alpha)
                BorderTable[move.reversed] = (lower, value);
            else if (value >= beta)
                BorderTable[move.reversed] = (value, upper);
            else
                BorderTable[move.reversed] = (value, value);

            return value;
        }

        long SearchedNodeCount;
        long CollidedCount;

        public Move[] CreateMoveArray(Board board, ulong moves, int count)
        {
            Move[] array = new Move[count];
            for (int i = 0; i < array.Length; i++)
            {
                ulong move = Board.NextMove(moves);
                moves = Board.RemoveMove(moves, move);
                array[i] = new Move(board, move);
            }
            return array;
        }

        public override (int x, int y, ulong move) DecideMove(Board board, int stone)
        {
            CollidedCount = 0;
            SearchedNodeCount = 0;
            BorderTable = new Dictionary<Board, (float, float)>();

            var sw = System.Diagnostics.Stopwatch.StartNew();

            if (stone == -1)
                board = board.ColorFliped();

            ulong result;
            if (board.stoneCount > StoneCountDoFullSearch)
            {
                Console.WriteLine("必勝読み開始");
           //     result = NegascoutRoot(board, GetSearchDepth(board));
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
            Console.WriteLine($"Nodes : {CollidedCount}/{SearchedNodeCount}");

            (int x, int y) = Board.ToPos(result);

            return (x, y, result);
        }
    }
}
