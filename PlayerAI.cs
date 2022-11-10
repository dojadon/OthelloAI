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
        public CutoffParameters CutoffParameters { get; }
        public IDictionary<Board, (float, float)> Table { get; set; } = new Dictionary<Board, (float, float)>();
        public virtual bool IsCanceled { get; set; }

        public Search(CutoffParameters cutoffParameters)
        {
            CutoffParameters = cutoffParameters;
        }

        public virtual Move[] OrderMoves(Move[] moves, float depth)
        {
            Array.Sort(moves);
            return moves;
        }

        public void StoreTranspositionTable(Move move, float alpha, float beta, float lower, float upper, float value)
        {
            if (value <= alpha)
                Table[move.reversed] = (lower, value);
            else if (value >= beta)
                Table[move.reversed] = (value, upper);
            else
                Table[move.reversed] = (value, value);
        }

        public bool TryTranspositionCutoff(Move move, float depth, ref float alpha, ref float beta, out float lower, out float upper, ref float value)
        {
            if (depth <= PlayerAI.transposition || move.reversed.n_stone > PlayerAI.ordering_depth || !CutoffParameters.shouldTranspositionCut || !Table.ContainsKey(move.reversed))
            {
                lower = -1000000;
                upper = 1000000;
                return false;
            }

            (lower, upper) = Table[move.reversed];

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

        public virtual bool TryProbCutoff(PlayerAI player, Move move, float depth, int depthShallow, int alpha, int beta, int lower, int upper, ref int value)
        {
            if (player.Solve(this, move, depthShallow, lower - 1, lower) < lower)
            {
                value = alpha - 1;
                return true;
            }
            if (player.Solve(this, move, depthShallow, upper, upper + 1) > upper)
            {
                value = beta + 1;
                return true;
            }
            return false;
        }

        public virtual bool TryProbCutoff(PlayerAI player, Move move, int depth, int depthShallow, int border, int lower, int upper, ref int value)
        {
            if (player.NullWindowSearch(this, move, depthShallow, lower) < lower)
            {
                value = border - 1;
                return true;
            }
            if (player.NullWindowSearch(this, move, depthShallow, upper + 1) > upper)
            {
                value = border;
                return true;
            }
            return false;
        }
    }

    public class SearchIterativeDeepening : Search
    {
        public int DepthInterval { get; }
        public IDictionary<Board, (float, float)> TablePrev { get; }

        public IComparer<Move> Comparer { get; set; }

        public SearchIterativeDeepening(CutoffParameters cutoffParameters, IDictionary<Board, (float, float)> prev, int depthPrev) : base(cutoffParameters)
        {
            TablePrev = prev;
            DepthInterval = depthPrev;
            Comparer = new MoveComparer(prev);
        }

        public override Move[] OrderMoves(Move[] moves, float depth)
        {
            if (depth >= 4)
                Array.Sort(moves, Comparer);
            else
                Array.Sort(moves);

            return moves;
        }

        public override bool TryProbCutoff(PlayerAI player, Move move, float depth, int depthShallow, int alpha, int beta, int lower, int upper, ref int value)
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
            return base.TryProbCutoff(player, move, depth, depthShallow, alpha, beta, lower, upper, ref value);
        }

        public override bool TryProbCutoff(PlayerAI player, Move move, int depth, int depthShallow, int border, int lower, int upper, ref int value)
        {
            if (depth - depthShallow == DepthInterval && TablePrev.ContainsKey(move.reversed))
            {
                (float lowerPrev, float upperPrev) = TablePrev[move.reversed];

                if (upperPrev < lower)
                {
                    value = border - 1;
                    return true;
                }

                if (lowerPrev > upper)
                {
                    value = border;
                    return true;
                }
            }
            return base.TryProbCutoff(player, move, depth, depthShallow, border, lower, upper, ref value);
        }

        class MoveComparer : IComparer<Move>
        {
            const int INTERVAL = 200;

            IDictionary<Board, (float, float)> Dict { get; }

            public MoveComparer(IDictionary<Board, (float, float)> dict)
            {
                Dict = dict;
            }

            public float Eval(Move move)
            {
                if (Dict.TryGetValue(move.reversed, out (float min, float max) t))
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
                return Comparer<float>.Default.Compare(Eval(x), Eval(y));
            }
        }
    }

    public class SearchParallel : Search
    {
        public override bool IsCanceled => State.IsStopped;
        private ParallelLoopState State { get; }

        public SearchParallel(CutoffParameters cutoffParameters, ParallelLoopState state) : base(cutoffParameters)
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

    public enum SearchType
    {
        Normal,
        IterativeDeepening,
    }

    public class SearchParameters
    {
        public readonly int stage;
        public readonly SearchType type;

        public Func<int, float> Depth { get; set; } = _ => 0;
        public Func<int, bool> ShouldTranspositionCut { get; set; } = _ => true;
        public Func<int, bool> ShouldStoreTranspositionTable { get; set; } = _ => true;
        public Func<int, bool> ShouldProbCut { get; set; } = _ => false;

        public SearchParameters(int stage, SearchType type, Func<int, float> depth)
        {
            this.stage = stage;
            this.type = type;
            Depth = depth;
        }

        public SearchParameters(int stage, SearchType type, float depth) : this(stage, type, _ => depth)
        {
        }

        public CutoffParameters CreateCutoffParameters(int n)
        {
            return new CutoffParameters(ShouldTranspositionCut(n), ShouldStoreTranspositionTable(n), ShouldProbCut(n));
        }
    }

    public class PlayerAI : Player
    {
        public const int INF = 10000000;

        public SearchParameters[] Params { get; set; }
        public Evaluator Evaluator { get; set; }

        public List<float> Times { get; } = new List<float>();
        public long SearchedNodeCount { get; set; }
        public int[] NodeCount { get; } = new int[10];
        public int[] PrunedNodeCount { get; } = new int[10];

        public PlayerAI(Evaluator evaluator)
        {
            Evaluator = evaluator;
        }

        protected float EvalFinishedGame(Board board)
        {
            // SearchedNodeCount++;
            return board.GetStoneCountGap() * 10000;
        }

        public float Eval(Board board)
        {
            SearchedNodeCount++;

            //if (color == 0 ^ board.n_stone % 2 == 0)
            //{
            //    return -Evaluator.Eval(board.ColorFliped());
            //}
            //else
            {
                return Evaluator.Eval(board);
            }
        }

        public float CorrectEvaluation(float e)
        {
            if (Math.Abs(e) >= 10000)
                return e / 10000;
            else
                return e * Weights.WEIGHT_RANGE / 127.0F;
        }

        int color = 0;

        public override (int x, int y, ulong move) DecideMove(Board board, int stone)
        {
            (int x, int y, ulong move, _) = DecideMoveWithEvaluation(board, stone);
            return (x, y, move);
        }

        public (int x, int y, ulong move, float e) DecideMoveWithEvaluation(Board board, int stone)
        {
            SearchedNodeCount = 0;
            for(int i = 0; i < NodeCount.Length; i++)
            {
                NodeCount[i] = 0;
                PrunedNodeCount[i] = 0;
            }

            color = stone;
            if (stone == -1)
                board = board.ColorFliped();

            Evaluator.StartSearch(stone);

            ulong result = 0;
            float e = 0;

            foreach(var param in Params.OrderByDescending(p => p.stage))
            {
                if (board.n_stone < param.stage)
                    continue;

                if (param.Depth(board.n_stone) <= 0)
                    e = Eval(board);

                (result, e) = SolveRoot(board, param);
                break;
            }

            (int x, int y) = Board.ToPos(result);

            return (x, y, result, CorrectEvaluation(e));
        }

        public (ulong, float) SolveRoot(Board board, SearchParameters param) => param.type switch
        {
            SearchType.Normal => SolveRoot(new Search(param.CreateCutoffParameters(board.n_stone)), board, param.Depth(board.n_stone)),
            SearchType.IterativeDeepening => SolveIterativeDeepening(board, param.CreateCutoffParameters(board.n_stone), param.Depth(board.n_stone), 2, 3),
            _ => (0, 0)
        };

        public (ulong , float) SolveIterativeDeepening(Board board, CutoffParameters param, float depth, int interval, int times)
        {
            var search = new Search(param);
            float d = depth - interval * Math.Min(times - 1, (int) Math.Ceiling((double) depth / interval) - 1);

            while (true)
            {
                if (PrintInfo)
                {
                    Console.WriteLine();
                    Console.WriteLine($"Depth {d}");
                }

                (ulong move, float e) = SolveRoot(search, board, d);

                if (d >= depth)
                    return (move, e);

                search = new SearchIterativeDeepening(param, search.Table, interval);
                d += interval;
            }
        }

        public (ulong, int) SolveEndGame(Board board, CutoffParameters param)
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
                var s = new SearchParallel(param, state);
                if (0 < -Solve(s, m, 64, -1, -0))
                {
                    Interlocked.Add(ref value, 2);
                    state.Stop();
                }
            });
            return value;
        }

        public (ulong, float) SolveRoot(Search search, Board board, float depth)
        {
            Move root = new Move(board);

            if (root.n_moves <= 1)
            {
                return (root.moves, 0);
            }

            Move[] array = root.NextMoves();
            search.OrderMoves(array, depth);

            Move result = array[0];
            float max = -Solve(search, array[0], depth - 1, -INF, INF);
            float alpha = max;

            if (PrintInfo)
                Console.WriteLine($"{Board.ToPos(result.move)} : {max}");

            for (int i = 1; i < array.Length; i++)
            {
                Move move = array[i];

                float eval = -Solve(search, move, depth - 1, -alpha - 1, -alpha);

                if (alpha < eval)
                {
                    alpha = eval;
                    eval = -Solve(search, move, depth - 1, -INF, -alpha);
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

        public float NullWindowSearch(Search search, Move move, float depth, float border)
        {
            return -Solve(search, move, depth - 1, -border - 1, -border);
        }

        public float Negascout(Search search, Board board, ulong moves, float depth, float alpha, float beta)
        {
            ulong move = Board.NextMove(moves);
            moves = Board.RemoveMove(moves, move);
            float max = -Solve(search, new Move(board, move), depth - 1, -beta, -alpha);

            if (beta <= max)
                return max;

            alpha = Math.Max(alpha, max);

            while ((move = Board.NextMove(moves)) != 0)
            {
                moves = Board.RemoveMove(moves, move);
                Move m = new Move(board, move);

                //int eval = -Solve(search, m, param, depth - 1, -alpha - 1, -alpha);
                float eval = NullWindowSearch(search, m, depth, alpha);

                if (beta <= eval)
                    return eval;

                if (alpha < eval)
                {
                    alpha = eval;
                    eval = -Solve(search, m, depth - 1, -beta, -alpha);

                    //ProbcutNode1[depth]++;

                    if (beta <= eval)
                    {
                       // ProbcutNode2[depth]++;
                        return eval;
                    }

                    alpha = Math.Max(alpha, eval);
                }
                max = Math.Max(max, eval);
            }
            return max;
        }

        public float Negascout(Search search, Move[] moves, float depth, float alpha, float beta)
        {
            float max = -Solve(search, moves[0], depth - 1, -beta, -alpha);

            if (beta <= max)
                return max;

            alpha = Math.Max(alpha, max);

            foreach (Move move in moves.AsSpan(1, moves.Length - 1))
            {
                //int eval = -Solve(search, move, param, depth - 1, -alpha - 1, -alpha);
                float eval = NullWindowSearch(search, move, depth, alpha);

                if (beta <= eval)
                    return eval;

                if (alpha < eval)
                {
                    alpha = eval;
                    eval = -Solve(search, move, depth - 1, -beta, -alpha);

                   // ProbcutNode1[depth]++;

                    if (beta <= eval)
                    {
                       // ProbcutNode2[depth]++;
                        return eval;
                    }

                    alpha = Math.Max(alpha, eval);
                }
                max = Math.Max(max, eval);
            }
            return max;
        }

        public float Negamax(Search search, Move[] moves, float depth, float alpha, float beta)
        {
            float max = -1000000;

            for (int i = 0; i < moves.Length; i++)
            {
                float e = -Solve(search, moves[i], depth - 1, -beta, -alpha);
                max = Math.Max(max, e);
                alpha = Math.Max(alpha, e);

                if (alpha >= beta)
                    return max;
            }
            return max;
        }

        public float Negamax(Search search, Board board, ulong moves, float depth, float alpha, float beta)
        {
            float max = -1000000;
            ulong move;
            while ((move = Board.NextMove(moves)) != 0)
            {
                moves = Board.RemoveMove(moves, move);

                float e = -Solve(search, new Move(board, move), depth - 1, -beta, -alpha);
                max = Math.Max(max, e);
                alpha = Math.Max(alpha, e);

                if (alpha >= beta)
                    return max;
            }
            return max;
        }

        public bool PrintInfo { get; set; } = true;

        public static int ordering_depth = 57;
        public static int transposition = 1;

        public virtual float Solve(Search search, Move move, float depth, float alpha, float beta)
        {
            if (search.IsCanceled)
                return -1000000;

            if(0 < depth && depth < 1)
            {
                if (depth > GA.GATest.Random.NextDouble())
                    depth = 1;
                else
                    depth = 0;
            }

            if (depth <= 0)
                return Eval(move.reversed);

            float value = 0;

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
                    return -Solve(search, next, depth, -beta, -alpha);
                }
            }

            if (move.reversed.n_stone == 63 && move.n_moves == 1)
                return -EvalFinishedGame(move.reversed.Reversed(move.moves));

            if (search.TryTranspositionCutoff(move, depth, ref alpha, ref beta, out float lower, out float upper, ref value))
                return value;

            if (depth >= 3 && move.reversed.n_stone < 60)
            {
                if (move.n_moves > 3)
                    value = Negascout(search, search.OrderMoves(move.NextMoves(), depth), depth, alpha, beta);
                else
                    value = Negascout(search, move.reversed, move.moves, depth, alpha, beta);
            }
            else
            {
                value = Negamax(search, move.reversed, move.moves, depth, alpha, beta);
            }

            if (depth > transposition && move.reversed.n_stone <= ordering_depth && search.CutoffParameters.shouldStoreTranspositionTable)
                search.StoreTranspositionTable(move, alpha, beta, lower, upper, value);

            return value;
        }
    }
}
