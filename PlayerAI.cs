using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OthelloAI
{
    public class Search
    {
        public Dictionary<Board, (float, float)> Table { get; set; } = new Dictionary<Board, (float, float)>();
        public Dictionary<Board, (float, float)> TablePrev { get; set; } = new Dictionary<Board, (float, float)>();

        public IComparer<Move> Comparer { get; set; }

        public virtual bool IsCanceled { get; set; }

        public void SwapTable()
        {
            TablePrev = Table;
            Comparer = new MoveComparer(TablePrev);
            Table = new Dictionary<Board, (float, float)>();
        }
    }

    public class SearchParallel : Search
    {
        public override bool IsCanceled => State.IsStopped;
        private ParallelLoopState State { get; }

        public SearchParallel(ParallelLoopState state)
        {
            State = state;
        }
    }

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

    public class PlayerAI : Player
    {
        public SearchParameters ParamBeg { get; set; }
        public SearchParameters ParamMid { get; set; }
        public SearchParameters ParamEnd { get; set; }

        public Evaluator Evaluator { get; set; }

        public long[] MCPCount { get; } = new long[60];
        public long[] SearchedNodeCount { get; } = new long[60];
        public long[] TranspositionTableCount { get; } = new long[60];

        public int CurrentIndex;

        public PlayerAI(Evaluator evaluator)
        {
            Evaluator = evaluator;
        }

        protected float EvalFinishedGame(Board board)
        {
            SearchedNodeCount[CurrentIndex]++;
            return board.GetStoneCountGap() * 10000;
        }

        protected float EvalLastMove(Board board)
        {
            SearchedNodeCount[CurrentIndex]++;
            return (board.GetStoneCountGap() + board.GetReversedCountOnLastMove()) * 10000;
        }

        public List<float> times = new List<float>();

        public float Eval(Board board)
        {
            return Evaluator.Eval(board);
        }

        public override (int x, int y, ulong move) DecideMove(Board board, int stone)
        {
            Search search = new Search();

            CurrentIndex = board.n_stone - 4;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            if (stone == -1)
                board = board.ColorFliped();

            ulong result;
            if (board.n_stone < ParamMid.stage)
            {
                (result, _) = SolveRoot(search, board, ParamBeg.cutoff_param, ParamBeg.depth);
            }
            else if (board.n_stone < ParamEnd.stage)
            {
                (result, _) = SolveRoot(search, board, ParamMid.cutoff_param, ParamMid.depth);
            }
            else if (board.n_stone < ParamEnd.stage + 4)
            {
                (result, _) = SolveEndGame(search, board, ParamEnd.cutoff_param);
            }
            else
            {
                (result, _) = SolveRoot(search, board, ParamEnd.cutoff_param, 64);
            }

            sw.Stop();

            if (result != 0)
            {
                float time = 1000F * sw.ElapsedTicks / System.Diagnostics.Stopwatch.Frequency;
                times.Add(time);

                if (PrintInfo)
                {
                    Console.WriteLine($"Taken Time : {time} ms");
                    Console.WriteLine($"Nodes : {TranspositionTableCount[CurrentIndex]}/{SearchedNodeCount[CurrentIndex]}");
                    Console.WriteLine($"MCP Count : {MCPCount[CurrentIndex]}");
                }
            }

            (int x, int y) = Board.ToPos(result);

            return (x, y, result);
        }

        public (ulong, float) SolveEndGame(Search search, Board board, CutoffParameters param)
        {
            Move root = new Move(board);

            if (root.n_moves == 0)
                return (0, 0);

            Move[] array = root.NextMoves();
            Array.Sort(array);

            for (int i = 1; i < array.Length; i++)
            {
                Move move = array[i];

                if (0 < -SolveEndGameParallel(move.NextMoves(), param))
                {
                    if (PrintInfo)
                        Console.WriteLine($"{Board.ToPos(move.move)} : Win");
                    return (move.move, 1);
                }
                else if (PrintInfo)
                {
                    Console.WriteLine($"{Board.ToPos(move.move)} : Lose");
                }
            }
            return (array[0].move, -1);
        }

        public float SolveEndGameParallel(Move[] array, CutoffParameters param)
        {
            Array.Sort(array);

            int value = -1;

            /* (int index, Move move, Search search)[][] nodes = array.Select((m, i) => m.NextMoves().Select(m_ => (i, m_, new Search())).ToArray()).ToArray();
             int[] count = new int[array.Length];

             Parallel.ForEach(nodes.SelectMany(array => array), (m, state) =>
             {
                 if (0 < -Solve(new SearchParallel(state), m.move, param, 64, -1, -0))
                 {
                     lock (nodes)
                         foreach ((_, Move move, Search search) in nodes[m.index])
                             search.IsCanceled = true;
                 }
                 else if (!m.search.IsCanceled)
                 {
                     lock (count)
                     {
                         count[m.index]++;
                         if (count[m.index] == nodes[m.index].Length)
                         {
                             Interlocked.Add(ref value, 2);

                             lock (nodes)
                                 foreach ((_, Move move, Search search) in nodes.SelectMany(a => a))
                                     search.IsCanceled = true;
                         }
                     }
                 }
             });*/

            Parallel.ForEach(array, (m, state) =>
            {
                var s = new SearchParallel(state);
                if (0 < -Solve(s, m, param, 64, -1, -0))
                {
                    Interlocked.Add(ref value, 2);
                    state.Stop();
                }
            });

            return value;
        }

        public (ulong, float) SolveRoot(Search search, Board board, CutoffParameters param, int depth)
        {
            Move root = new Move(board);

            if (root.n_moves <= 1)
            {
                return (root.moves, 0);
            }

            Move[] array = root.NextMoves();

            Array.Sort(array);

            Move result = array[0];
            float max = -Solve(search, array[0], param, depth - 1, -1000000, 1000000);
            float alpha = max;

            if (PrintInfo)
                Console.WriteLine($"{Board.ToPos(result.move)} : {max}");

            for (int i = 1; i < array.Length; i++)
            {
                Move move = array[i];

                float eval = -Solve(search, move, param, depth - 1, -alpha - 1, -alpha);

                if (alpha < eval)
                {
                    alpha = eval;
                    eval = -Solve(search, move, param, depth - 1, -1000000, -alpha);
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
            return (result.move, max);
        }

        public float Negascout(Search search, Move[] moves, CutoffParameters param, int depth, float alpha, float beta)
        {
            float max = -Solve(search, moves[0], param, depth - 1, -beta, -alpha);

            if (beta <= max)
                return max;

            alpha = Math.Max(alpha, max);

            for (int i = 1; i < moves.Length; i++)
            {
                Move move = moves[i];

                float eval = -Solve(search, move, param, depth - 1, -alpha - 1, -alpha);

                if (beta <= eval)
                    return eval;

                if (alpha < eval)
                {
                    alpha = eval;
                    eval = -Solve(search, move, param, depth - 1, -beta, -alpha);

                    if (beta <= eval)
                        return eval;

                    alpha = Math.Max(alpha, eval);
                }
                max = Math.Max(max, eval);
            }
            return max;
        }

        public float Negamax(Search search, Move[] moves, CutoffParameters param, int depth, float alpha, float beta)
        {
            for (int i = 0; i < moves.Length; i++)
            {
                alpha = Math.Max(alpha, -Solve(search, moves[i], param, depth - 1, -beta, -alpha));

                if (alpha >= beta)
                    return alpha;
            }
            return alpha;
        }

        public float Negamax(Search search, Board board, ulong moves, CutoffParameters param, int depth, float alpha, float beta)
        {
            ulong move;
            while ((move = Board.NextMove(moves)) != 0)
            {
                moves = Board.RemoveMove(moves, move);

                alpha = Math.Max(alpha, -Solve(search, new Move(board, move), param, depth - 1, -beta, -alpha));

                if (alpha >= beta)
                    return alpha;
            }
            return alpha;
        }

        public bool PrintInfo { get; set; } = true;

        public bool TryTranspositionCutoff(Dictionary<Board, (float, float)> table, Move move, CutoffParameters param, ref float alpha, ref float beta, out float lower, out float upper, ref float value)
        {
            if (move.reversed.n_stone > ordering_depth || !param.shouldTranspositionCut || !table.ContainsKey(move.reversed))
            {
                lower = -1000000;
                upper = 1000000;
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

        public bool TryProbCutoff(Search search, Move move, CutoffParameters param, int depth, float alpha, float beta, ref float value)
        {
            if (!param.shouldProbCut || depth < 5 || depth > 8)
                return false;

            float sigma = (3 * move.reversed.n_stone - 8) * 3.2F;
            float offset = move.reversed.n_stone * (depth % 2 == 0 ? -2 : 2);
            float e = alpha - sigma - offset;
            CutoffParameters mcpParam = new CutoffParameters(param.shouldTranspositionCut, false, true);

            if (Solve(search, move, mcpParam, depth - 3, e - 1, e) < e)
            {
                MCPCount[CurrentIndex]++;
                value = alpha;
                return true;
            }
            e = beta + sigma - offset;
            if (Solve(search, move, mcpParam, depth - 3, e, e + 1) > e)
            {
                MCPCount[CurrentIndex]++;
                value = beta;
                return true;
            }
            return false;
        }

        int ordering_depth = 57;

        public float Solve(Search search, Move move, CutoffParameters param, int depth, float alpha, float beta)
        {
            //SearchedNodeCount[CurrentIndex]++;

            if (search.IsCanceled)
                return -1000000;

            if (depth <= 0)
                return Eval(move.reversed);

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
                    return -Solve(search, next, param, depth, -beta, -alpha);
                }
            }

            float value = 0;
            if (TryTranspositionCutoff(search.Table, move, param, ref alpha, ref beta, out float lower, out float upper, ref value))
                return value;

            /*if (TryProbCutoff(search, move, param, depth, alpha, beta, ref value))
                return value;*/

            if (move.n_moves == 1)
            {
                if (move.reversed.n_stone == 63)
                    //return EvalLastMove(move.reversed);
                    return -EvalFinishedGame(move.reversed.Reversed(move.moves));
                else
                {
                    return -Solve(search, new Move(move.reversed, move.moves), param, depth - 1, -beta, -alpha);
                }
            }

            if (move.n_moves > 2 && depth >= 3 && move.reversed.n_stone < 60)
            {
                Move[] array = move.NextMoves();
                Array.Sort(array);
                value = Negascout(search, array, param, depth, alpha, beta);
            }
            else
            {
                value = Negamax(search, move.reversed, move.moves, param, depth, alpha, beta);
            }

            if (move.reversed.n_stone <= ordering_depth && param.shouldStoreTranspositionTable)
                StoreTranspositionTable(search.Table, move, alpha, beta, lower, upper, value);

            return value;
        }
    }

    public readonly struct MoveComparer : IComparer<Move>
    {
        Dictionary<Board, (float, float)> Dict { get; }

        public MoveComparer(Dictionary<Board, (float, float)> dict)
        {
            Dict = dict;
        }

        public int Test(in Board board)
        {
            if (!Dict.ContainsKey(board))
                return -10000000;

            (float a, float b) = Dict[board];
            return a == b ? (int)a : -10000000;
        }

        public int Compare([AllowNull] Move x, [AllowNull] Move y)
        {
            return Test(y.reversed) - Test(x.reversed);
        }
    }
}
