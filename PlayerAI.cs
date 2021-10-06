using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OthelloAI
{
    public class Search
    {
        public IDictionary<Board, (int, int)> Table { get; set; } = new Dictionary<Board, (int, int)>();
        public virtual bool IsCanceled { get; set; }

        public virtual Move[] OrderMoves(Move[] moves, int depth)
        {
            Array.Sort(moves);
            return moves;
        }

        public virtual bool TryCutoffOrUpdateBorder(Move move, CutoffParameters param, int depth, ref float alpha, ref float beta, ref float value)
        {
            return false;
        }

        public void StoreTranspositionTable(IDictionary<Board, (int, int)> table, Move move, int alpha, int beta, int lower, int upper, int value)
        {
            if (value <= alpha)
                table[move.reversed] = (lower, value);
            else if (value >= beta)
                table[move.reversed] = (value, upper);
            else
                table[move.reversed] = (value, value);
        }

        public virtual bool TryProbCutoff(PlayerAI player, Move move, CutoffParameters param, int depth, int depthShallow, int alpha, int beta, int lower, int upper, ref int value)
        {
            if (player.Solve(this, move, param, depthShallow, lower - 1, lower) < lower)
            {
                value = alpha - 1;
                return true;
            }
            if (player.Solve(this, move, param, depthShallow, upper, upper + 1) > upper)
            {
                value = beta + 1;
                return true;
            }
            return false;
        }

        public virtual bool TryProbCutoff(PlayerAI player, Move move, CutoffParameters param, int depth, int depthShallow, int border, int lower, int upper, ref int value)
        {
            if (player.NullWindowSearch(this, move, param, depthShallow, lower) < lower)
            {
                value = border - 1;
                return true;
            }
            if (player.NullWindowSearch(this, move, param, depthShallow, upper) > upper)
            {
                value = border + 1;
                return true;
            }
            return false;
        }
    }

    public class SearchIterativeDeepening : Search
    {
        public int DepthInterval { get; }
        public IDictionary<Board, (int, int)> TablePrev { get; }

        public IComparer<Move> Comparer { get; set; }

        public SearchIterativeDeepening(IDictionary<Board, (int, int)> prev, int depthPrev)
        {
            TablePrev = prev;
            DepthInterval = depthPrev;
            Comparer = new MoveComparer(prev);
        }

        public override Move[] OrderMoves(Move[] moves, int depth)
        {
            if (depth >= 4)
                Array.Sort(moves, Comparer);
            else
                Array.Sort(moves);

            return moves;
        }

        public override bool TryProbCutoff(PlayerAI player, Move move, CutoffParameters param, int depth, int depthShallow, int alpha, int beta, int lower, int upper, ref int value)
        {
            if (depth - depthShallow == DepthInterval && TablePrev.ContainsKey(move.reversed))
            {
                (float lowerPrev, float upperPrev) = TablePrev[move.reversed];

                if (upperPrev < lower)
                {
                    value = alpha - 1;
                    return true;
                }

                if (lowerPrev > upper)
                {
                    value = beta + 1;
                    return true;
                }
            }
            return base.TryProbCutoff(player, move, param, depth, depthShallow, alpha, beta, lower, upper, ref value);
        }

        class MoveComparer : IComparer<Move>
        {
            const int INTERVAL = 200;

            IDictionary<Board, (int, int)> Dict { get; }

            public MoveComparer(IDictionary<Board, (int, int)> dict)
            {
                Dict = dict;
            }

            public int Eval(Move move)
            {
                if (Dict.TryGetValue(move.reversed, out (int min, int max) t))
                {
                    if (-PlayerAI.INF < t.min && t.max < PlayerAI.INF)
                        return (t.min + t.max) / 2;
                    else if (-PlayerAI.INF < t.min)
                        return t.min / 2 + INTERVAL;
                    else if (PlayerAI.INF > t.max)
                        return t.max / 2 - INTERVAL;
                }
                return PlayerAI.INF + move.n_moves;
            }

            public int Compare([AllowNull] Move x, [AllowNull] Move y)
            {
                return Eval(x) - Eval(y);
            }
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
        public const int INF = 1000000;

        public SearchParameters ParamBeg { get; set; }
        public SearchParameters ParamMid { get; set; }
        public SearchParameters ParamEnd { get; set; }

        public Evaluator Evaluator { get; set; }

        public List<float> Times { get; } = new List<float>();
        public long SearchedNodeCount { get; set; }

        public PlayerAI(Evaluator evaluator)
        {
            Evaluator = evaluator;
        }

        protected int EvalFinishedGame(Board board)
        {
            SearchedNodeCount++;
            return board.GetStoneCountGap() * 10000;
        }

        protected int EvalLastMove(Board board)
        {
            SearchedNodeCount++;
            return (board.GetStoneCountGap() + board.GetReversedCountOnLastMove()) * 10000;
        }

        public int Eval(Board board)
        {
            SearchedNodeCount++;
            return Evaluator.Eval(board);
        }

        public override (int x, int y, ulong move) DecideMove(Board board, int stone)
        {
            mpc = 0;
            SearchedNodeCount = 0;
            for (int i = 0; i < ProbcutNode1.Length; i++)
            {
                ProbcutNode1[i] = ProbcutNode2[i] = ProbcutNode3[i] = ProbcutNode4[i] = ProbcutNode5[i] = 0;
            }

            Search search = new Search();

            var sw = System.Diagnostics.Stopwatch.StartNew();

            if (stone == -1)
                board = board.ColorFliped();

            ulong result;
            if (board.n_stone < ParamMid.stage)
            {
                result = SolveIterativeDeepening(board, ParamBeg.cutoff_param, ParamBeg.depth, 2, 3);
                //(result, _) = SolveRoot(search, board, ParamBeg.cutoff_param, ParamBeg.depth);
            }
            else if (board.n_stone < ParamEnd.stage)
            {
                result = SolveIterativeDeepening(board, ParamMid.cutoff_param, ParamMid.depth, 2, 3);
                // (result, _) = SolveRoot(search, board, ParamMid.cutoff_param, ParamMid.depth);
            }
            /*else if (board.n_stone < ParamEnd.stage + 4)
            {
                (result, _) = SolveEndGame(search, board, ParamEnd.cutoff_param);
            }*/
            else
            {
                (result, _) = SolveRoot(search, board, ParamEnd.cutoff_param, 64);
            }

            sw.Stop();

            if (result != 0)
            {
                float time = 1000F * sw.ElapsedTicks / System.Diagnostics.Stopwatch.Frequency;
                Times.Add(time);

                if (PrintInfo)
                {
                    Console.WriteLine($"Taken Time : {time} ms");
                    Console.WriteLine($"Nodes : {SearchedNodeCount}");
                    Console.WriteLine($"MPC : {mpc}");

                    for (int i = 0; i < ProbcutNode1.Length; i++)
                    {
                        if (ProbcutNode1[i] == 0)
                            continue;

                        Console.WriteLine($"Depth {i}: True {100F * ProbcutNode2[i] / ProbcutNode1[i]}%, {ProbcutNode2[i]}/{ProbcutNode1[i]}");

                        /*Console.WriteLine();
                        Console.WriteLine($"Depth {i}: False Posi {100F * ProbcutNode2[i] / ProbcutNode1[i]}%, {ProbcutNode2[i]}/{ProbcutNode1[i]}");
                        Console.WriteLine($"Depth {i}: False Nega {100F * ProbcutNode3[i] / ProbcutNode1[i]}%, {ProbcutNode3[i]}/{ProbcutNode1[i]}");
                        Console.WriteLine($"Depth {i}: True Nega {100F * ProbcutNode4[i] / ProbcutNode1[i]}%, {ProbcutNode4[i]}/{ProbcutNode1[i]}");
                        Console.WriteLine($"Depth {i}: True Posi {100F * ProbcutNode5[i] / ProbcutNode1[i]}%, {ProbcutNode5[i]}/{ProbcutNode1[i]}");
                        foreach (var s in test[i])
                            Console.WriteLine(s);*/
                    }
                }
            }

            (int x, int y) = Board.ToPos(result);

            return (x, y, result);
        }

        public ulong SolveIterativeDeepening(Board board, CutoffParameters param, int depth, int interval, int times)
        {
            var search = new Search();
            int d = depth - interval * (times - 1);

            while (true)
            {
                if (PrintInfo)
                {
                    Console.WriteLine();
                    Console.WriteLine($"Depth {d}");
                }

                (ulong move, _) = SolveRoot(search, board, param, d);

                if (d >= depth)
                    return move;

                search = new SearchIterativeDeepening(search.Table, interval);
                d += interval;
            }
        }

        public (ulong, int) SolveEndGame(Search search, Board board, CutoffParameters param)
        {
            Move root = new Move(board);

            if (root.n_moves == 0)
                return (0, 0);

            Move[] array = root.NextMoves();
            Array.Sort(array);

            for (int i = 0; i < array.Length; i++)
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

        public int SolveEndGameParallel(Move[] array, CutoffParameters param)
        {
            Array.Sort(array);

            int value = -1;

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

        public (ulong, int) SolveRoot(Search search, Board board, CutoffParameters param, int depth)
        {
            Move root = new Move(board);

            if (root.n_moves <= 1)
            {
                return (root.moves, 0);
            }

            Move[] array = root.NextMoves();
            search.OrderMoves(array, depth);

            Move result = array[0];
            int max = -Solve(search, array[0], param, depth - 1, -1000000, 1000000);
            int alpha = max;

            if (PrintInfo)
                Console.WriteLine($"{Board.ToPos(result.move)} : {max}");

            for (int i = 1; i < array.Length; i++)
            {
                Move move = array[i];

                int eval = -Solve(search, move, param, depth - 1, -alpha - 1, -alpha);

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
                    Console.WriteLine($"{Board.ToPos(move.move)} : Pruned");
                }

                if (max < eval)
                {
                    max = eval;
                    result = move;
                }
            }
            return (result.move, max);
        }

        public int NullWindowSearch(Search search, Move move, CutoffParameters param, int depth, int border)
        {
            int lower, upper;
            lower = border - 800;
            upper = border + 800;
            //(lower, upper) = Program.MCP_PARAM2.Test(depth - 2, move.reversed.n_stone, border, border, 2F);

            int v = 0;
           /* if (move.reversed.n_stone > 16 && 4 < depth && depth < 9 && search.TryProbCutoff(this, move, param, depth, depth - 2, border, lower, upper, ref v))
            {
            }
            else
            {
                v = -Solve(search, move, param, depth - 1, -border - 1, -border);
            }
            return v;*/

            int value = -Solve(search, move, param, depth - 1, -border - 1, -border);

            if (move.reversed.n_stone > 16 && 4 < depth  && depth < 9 && search.TryProbCutoff(this, move, param, depth, depth - 2, border, lower, upper, ref v))
            {
                ProbcutNode1[depth]++;

                if (value < border == v < border)
                    ProbcutNode2[depth]++;
            }
            return value;
        }

        public int Negascout(Search search, Board board, ulong moves, CutoffParameters param, int depth, int alpha, int beta)
        {
            ulong move = Board.NextMove(moves);
            moves = Board.RemoveMove(moves, move);
            int max = -Solve(search, new Move(board, move), param, depth - 1, -beta, -alpha);

            if (beta <= max)
                return max;

            alpha = Math.Max(alpha, max);

            while ((move = Board.NextMove(moves)) != 0)
            {
                moves = Board.RemoveMove(moves, move);
                Move m = new Move(board, move);

                //int eval = -Solve(search, m, param, depth - 1, -alpha - 1, -alpha);
                int eval = NullWindowSearch(search, m, param, depth, alpha);

                if (beta <= eval)
                    return eval;

                if (alpha < eval)
                {
                    alpha = eval;
                    eval = -Solve(search, m, param, depth - 1, -beta, -alpha);

                    if (beta <= eval)
                        return eval;

                    alpha = Math.Max(alpha, eval);
                }
                max = Math.Max(max, eval);
            }
            return max;
        }

        public int Negascout(Search search, Move[] moves, CutoffParameters param, int depth, int alpha, int beta)
        {
            int max = -Solve(search, moves[0], param, depth - 1, -beta, -alpha);

            if (beta <= max)
                return max;

            alpha = Math.Max(alpha, max);

            foreach (Move move in moves.AsSpan(1, moves.Length - 1))
            {
                //int eval = -Solve(search, move, param, depth - 1, -alpha - 1, -alpha);
                int eval = NullWindowSearch(search, move, param, depth, alpha);

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

        public int Negamax(Search search, Move[] moves, CutoffParameters param, int depth, int alpha, int beta)
        {
            int max = -1000000;

            for (int i = 0; i < moves.Length; i++)
            {
                int e = -Solve(search, moves[i], param, depth - 1, -beta, -alpha);
                max = Math.Max(max, e);
                alpha = Math.Max(alpha, e);

                if (alpha >= beta)
                    return max;
            }
            return max;
        }

        public int Negamax(Search search, Board board, ulong moves, CutoffParameters param, int depth, int alpha, int beta)
        {
            int max = -1000000;
            ulong move;
            while ((move = Board.NextMove(moves)) != 0)
            {
                moves = Board.RemoveMove(moves, move);

                int e = -Solve(search, new Move(board, move), param, depth - 1, -beta, -alpha);
                max = Math.Max(max, e);
                alpha = Math.Max(alpha, e);

                if (alpha >= beta)
                    return max;
            }
            return max;
        }

        public bool PrintInfo { get; set; } = true;

        public bool TryTranspositionCutoff(IDictionary<Board, (int, int)> table, Move move, CutoffParameters param, int depth, ref int alpha, ref int beta, out int lower, out int upper, ref int value)
        {
            if (depth <= transposition || move.reversed.n_stone > ordering_depth || !param.shouldTranspositionCut || !table.ContainsKey(move.reversed))
            {
                lower = -1000000;
                upper = 1000000;
                return false;
            }

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

        public void StoreTranspositionTable(IDictionary<Board, (int, int)> table, Move move, int alpha, int beta, int lower, int upper, int value)
        {
            if (value <= alpha)
                table[move.reversed] = (lower, value);
            else if (value >= beta)
                table[move.reversed] = (value, upper);
            else
                table[move.reversed] = (value, value);
        }

        public int[] ProbcutNode1 = new int[10];
        public int[] ProbcutNode2 = new int[10];
        public int[] ProbcutNode3 = new int[10];
        public int[] ProbcutNode4 = new int[10];
        public int[] ProbcutNode5 = new int[10];

        static string Visualize(float range, float alpha, float beta, float lower, float upper, float shallow, float value)
        {
            int length = 50;
            float center = (lower + upper) / 2;

            string[] array = new string[length];

            bool add(float v, char c)
            {
                float t = (v + range) / range / 2;
                int idx = (int)(length * (v + range) / range / 2);

                if (idx < 0)
                    idx = 0;

                if (length <= idx)
                    idx = length - 1;

                array[idx] += c;

                return true;
            }

            List<(float v, char c)> list = new List<(float, char)>() { (lower, '['), (alpha, '('), /*(shallow, '+'), */(value, '*'), (beta, ')'), (upper, ']') };
            foreach (var (v, c) in list.OrderBy(t => t.v))
            {
                add(v, c);
            }

            return string.Join("_", array);
        }

        List<string>[] test = Enumerable.Range(0, 9).Select(i => new List<string>()).ToArray();

        public bool TryProbCutoff(Search search, Move move, CutoffParameters param, int depth, int alpha, int beta, ref int value)
        {
            if (Math.Abs(alpha - beta) <= 1 || !param.shouldProbCut || depth < 5 || depth > 8)
                return false;

            int lower, upper;
            (lower, upper) = Program.MCP_PARAM2.Test(depth - 2, move.reversed.n_stone, alpha, beta, 1.5F);

            return search.TryProbCutoff(this, move, param, depth, depth - 2, alpha, beta, lower, upper, ref value);

            ProbcutNode1[depth]++;

            int v = 0;
            bool probcut = search.TryProbCutoff(this, move, param, depth, depth - 2, alpha, beta, lower, upper, ref v);
            value = Solve(search, move, new CutoffParameters(true, false, false), depth, alpha, beta);
            bool cut = value <= alpha || beta < value;

            //if(Math.Abs(alpha - beta) <= 1)
            {
                string msg = Visualize(320, alpha, beta, lower, upper, v, value);

                if (!cut && probcut)
                {
                    ProbcutNode2[depth]++;
                    test[depth].Add("False Posi " + msg);
                }
                else if (cut && !probcut)
                {
                    ProbcutNode3[depth]++;
                    test[depth].Add("False Nega " + msg);
                }
                else if (cut)
                {
                    ProbcutNode5[depth]++;
                    test[depth].Add("True  Posi " + msg);
                }
                else
                {
                    ProbcutNode4[depth]++;
                    test[depth].Add("True  Nega " + msg);
                }
            }

            return cut;
        }

        int ordering_depth = 57;
        int transposition = 1;

        int mpc = 0;

        public virtual int Solve(Search search, Move move, CutoffParameters param, int depth, int alpha, int beta)
        {
            if (search.IsCanceled)
                return -1000000;

            if (depth <= 0)
                return Eval(move.reversed);

            int value = 0;

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

            if (move.reversed.n_stone == 63 && move.n_moves == 1)
                return -EvalFinishedGame(move.reversed.Reversed(move.moves));

            if (TryTranspositionCutoff(search.Table, move, param, depth, ref alpha, ref beta, out int lower, out int upper, ref value))
                return value;

            /*if (TryProbCutoff(search, move, param, depth, alpha, beta, ref value))
            {
                mpc++;
                return value;
            }*/

            if (depth >= 3 && move.reversed.n_stone < 60)
            {
                if (move.n_moves > 3)
                    value = Negascout(search, search.OrderMoves(move.NextMoves(), depth), param, depth, alpha, beta);
                else
                    value = Negascout(search, move.reversed, move.moves, param, depth, alpha, beta);
            }
            else
            {
                value = Negamax(search, move.reversed, move.moves, param, depth, alpha, beta);
            }

            if (depth > transposition && move.reversed.n_stone <= ordering_depth && param.shouldStoreTranspositionTable)
                StoreTranspositionTable(search.Table, move, alpha, beta, lower, upper, value);

            return value;
        }
    }
}
