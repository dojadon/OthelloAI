using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace OthelloAI
{
    public readonly struct CutoffParameters
    {
        public readonly bool shouldTranspositionCut;
        public readonly bool shouldStoreTranspositionTable;
        public readonly bool shouldProbCut;

        public CutoffParameters(bool transposition, bool storeTransposition, bool probcut)
        {
            shouldTranspositionCut = transposition;
            shouldStoreTranspositionTable = storeTransposition;
            shouldProbCut = probcut;
        }
    }

    public class SearchParameters
    {
        public readonly int depth;
        public readonly int stage;
        public readonly CutoffParameters cutoff_param;

        public SearchParameters(int depth, int stage, CutoffParameters cutoff_param)
        {
            this.depth = depth;
            this.stage = stage;
            this.cutoff_param = cutoff_param;
        }
    }

    public class PlayerNegascout : Player
    {
        public SearchParameters ParamBeg { get; set; }
        public SearchParameters ParamMid { get; set; }
        public SearchParameters ParamEnd { get; set; }

        public Evaluator Evaluator { get; set; }

        public PlayerNegascout(Evaluator evaluator)
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

        public List<float> times = new List<float>();

        public float Eval(Board board)
        {
            return Evaluator.Eval(board);
        }

        public ulong NegascoutRoot(Dictionary<Board, (float, float)> table, Board board, CutoffParameters param, int depth)
        {
            Move root = new Move(board);

            if (root.n_moves <= 1)
            {
                return root.moves;
            }

            Move[] array = root.NextMoves();

            Array.Sort(array);

            Move result = array[0];
            float max = -Search(table, array[0], param, depth - 1, -1000000, 1000000);
            float alpha = max;

            if (PrintInfo)
                Console.WriteLine($"{Board.ToPos(result.move)} : {max}");

            for (int i = 1; i < array.Length; i++)
            {
                Move move = array[i];

                float eval = -Search(table, move, param, depth - 1, -alpha - 1, -alpha);

                if (alpha < eval)
                {
                    alpha = eval;
                    eval = -Search(table, move, param, depth - 1, -1000000, -alpha);
                    alpha = Math.Max(alpha, eval);

                    if (PrintInfo)
                        Console.WriteLine($"{Board.ToPos(move.move)} : {eval}");
                }
                else if (PrintInfo)
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

        public float Negascout(Dictionary<Board, (float, float)> table, Move[] moves, CutoffParameters param, int depth, float alpha, float beta)
        {
            float max = -Search(table, moves[0], param, depth - 1, -beta, -alpha);

            if (beta <= max)
                return max;

            alpha = Math.Max(alpha, max);

            for (int i = 1; i < moves.Length; i++)
            {
                Move move = moves[i];

                float eval = -Search(table, move, param, depth - 1, -alpha - 1, -alpha);

                if (beta <= eval)
                    return eval;

                if (alpha < eval)
                {
                    alpha = eval;
                    eval = -Search(table, move, param, depth - 1, -beta, -alpha);

                    if (beta <= eval)
                        return eval;

                    alpha = Math.Max(alpha, eval);
                }
                max = Math.Max(max, eval);
            }
            return max;
        }

        public float Negamax(Dictionary<Board, (float, float)> table, Move[] moves, CutoffParameters param, int depth, float alpha, float beta)
        {
            for (int i = 0; i < moves.Length; i++)
            {
                alpha = Math.Max(alpha, -Search(table, moves[i], param, depth - 1, -beta, -alpha));

                if (alpha >= beta)
                    return alpha;
            }
            return alpha;
        }

        public int[] MCPCount { get; } = new int[60];
        public int[] SearchedNodeCount { get; } = new int[60];
        public int[] TranspositionTableCount { get; } = new int[60];

        public int CurrentIndex;

        public bool PrintInfo { get; set; } = true;

        public bool TryTranspositionCutoff(Dictionary<Board, (float, float)> table, Move move, ref float alpha, ref float beta, ref float lower, ref float upper, ref float value)
        {
            if (!table.ContainsKey(move.reversed))
            {
                return false;
            }
            TranspositionTableCount[CurrentIndex]++;

            (lower, upper) = table[move.reversed];

            if (lower >= beta)
            {
                value = lower;
                return true;
            }

            if (upper <= alpha || upper == lower)
            {
                value = upper;
                return true;
            }

            alpha = Math.Max(alpha, lower);
            beta = Math.Min(beta, upper);

            return false;
        }

        public void StoreTranspositionTable(Dictionary<Board, (float, float)> table, Move move, float alpha, float beta, float lower, float upper, float value)
        {
            if (value <= alpha)
                table[move.reversed] = (lower, value);
            else if (value >= beta)
                table[move.reversed] = (value, upper);
            else
                table[move.reversed] = (value, value);
        }

        public bool TryProbCutoff(Dictionary<Board, (float, float)> table, Move move, CutoffParameters param, int depth, float alpha, float beta, ref float value)
        {
            if (depth < 5 || depth > 8)
                return false;

            float sigma = (3 * move.reversed.stoneCount - 8) * 3.2F;
            float offset = move.reversed.stoneCount * (depth % 2 == 0 ? -2 : 2);
            float e = alpha - sigma - offset;
            CutoffParameters mcpParam = new CutoffParameters(param.shouldTranspositionCut, false, true);

            if (Search(table, move, mcpParam, depth - 3, e - 1, e) < e)
            {
                MCPCount[CurrentIndex]++;
                value = alpha;
                return true;
            }
            e = beta + sigma - offset;
            if (Search(table, move, mcpParam, depth - 3, e, e + 1) > e)
            {
                MCPCount[CurrentIndex]++;
                value = beta;
                return true;
            }
            return false;
        }

        public float Search(Dictionary<Board, (float, float)> table, Move move, CutoffParameters param, int depth, float alpha, float beta)
        {
            SearchedNodeCount[CurrentIndex]++;

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
                    Move next = new Move(move.reversed.ColorFliped(), 0, opponentMoves, Board.BitCount(opponentMoves));
                    return -Search(table, next, param, depth, -beta, -alpha);
                }
            }

            float value = 0;

            if (param.shouldProbCut && TryProbCutoff(table, move, param, depth, alpha, beta, ref value))
                return value;

            float lower = -1000000;
            float upper = 1000000;

            if (param.shouldTranspositionCut && TryTranspositionCutoff(table, move, ref alpha, ref beta, ref lower, ref upper, ref value))
                return value;

            if (move.n_moves == 1)
            {
                if (move.reversed.stoneCount == 63)
                {
                    //return EvalLastMove(move.reversed);
                    return -EvalFinishedGame(move.reversed.Reversed(move.moves));
                }
                else
                {
                    return -Search(table, new Move(move.reversed, move.moves), param, depth - 1, -beta, -alpha);
                }
            }

            Move[] array = move.NextMoves();

            if (move.n_moves > 3 && depth >= 3 && move.reversed.stoneCount < 58)
            {
                Array.Sort(array);
                value = Negascout(table, array, param, depth, alpha, beta);
            }
            else
            {
                value = Negamax(table, array, param, depth, alpha, beta);
            }

            if (param.shouldStoreTranspositionTable)
                StoreTranspositionTable(table, move, alpha, beta, lower, upper, value);

            return value;
        }

        public override (int x, int y, ulong move) DecideMove(Board board, int stone)
        {
            CurrentIndex = board.stoneCount - 4;
            var transpositionTable = new Dictionary<Board, (float, float)>();

            var sw = System.Diagnostics.Stopwatch.StartNew();

            if (stone == -1)
                board = board.ColorFliped();

            ulong result;
            if(board.stoneCount < ParamMid.stage)
            {
                result = NegascoutRoot(transpositionTable, board, ParamBeg.cutoff_param, ParamBeg.depth);
            }
            else if (board.stoneCount < ParamEnd.stage)
            {
                result = NegascoutRoot(transpositionTable, board, ParamMid.cutoff_param, ParamMid.depth);
            }
            else
            {
                result = NegascoutRoot(transpositionTable, board, ParamEnd.cutoff_param, ParamEnd.depth);
            }

            sw.Stop();

            if (result != 0)
            {
                float time = 1000F * sw.ElapsedTicks / System.Diagnostics.Stopwatch.Frequency;
                times.Add(time);

                if(PrintInfo)
                {
                    Console.WriteLine($"Taken Time : {time} ms");
                    Console.WriteLine($"Nodes : {TranspositionTableCount[CurrentIndex]}/{SearchedNodeCount[CurrentIndex]}");
                    Console.WriteLine($"MCP Count : {MCPCount[CurrentIndex]}");
                }
            }

            (int x, int y) = Board.ToPos(result);

            return (x, y, result);
        }
    }
}
