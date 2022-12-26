using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OthelloAI
{
    public class Search
    {
        public IDictionary<Board, (float, float)> Table { get; set; } = new Dictionary<Board, (float, float)>();
        public virtual bool IsCanceled { get; set; }

        public Search()
        {
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

        public bool TryTranspositionCutoff(Move move, ref SearchParameter p, out float lower, out float upper, ref float value)
        {
            if (p.depth <= PlayerAI.transposition || move.reversed.n_stone > PlayerAI.ordering_depth || !p.transposition_cut || !Table.ContainsKey(move.reversed))
            {
                lower = -1000000;
                upper = 1000000;
                return false;
            }

            (lower, upper) = Table[move.reversed];

            if (lower >= p.beta)
            {
                value = lower;
                return true;
            }

            if (upper <= p.alpha || upper == lower)
            {
                value = upper;
                return true;
            }

            p.alpha = Math.Max(p.alpha, lower);
            p.beta = Math.Min(p.beta, upper);

            return false;
        }
    }

    public class SearchIterativeDeepening : Search
    {
        public int DepthInterval { get; }
        public IDictionary<Board, (float, float)> TablePrev { get; }

        public IComparer<Move> Comparer { get; set; }

        public SearchIterativeDeepening(IDictionary<Board, (float, float)> prev, int depthPrev) : base()
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

        public SearchParallel(ParallelLoopState state) : base()
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

    public struct SearchParameter
    {
        public float depth;
        public bool depth_prob_cut;

        public float alpha, beta;

        public readonly bool transposition_cut, store_transposition;

        public SearchParameter(float depth, bool depth_prob_cut, float alpha, float beta, bool transposition_cut, bool store_transposition)
        {
            this.depth = depth;
            this.depth_prob_cut = depth_prob_cut;
            this.alpha = alpha;
            this.beta = beta;
            this.transposition_cut = transposition_cut;
            this.store_transposition = store_transposition;
        }

        public static SearchParameter CreateInitParam(float depth, bool depth_prob_cut, bool transposition_cut, bool store_transposition)
        {
            return new SearchParameter(depth, depth_prob_cut, -PlayerAI.INF, PlayerAI.INF, transposition_cut, store_transposition);
        }

        public SearchParameter Deepen()
        {
            return new SearchParameter(depth - 1, depth_prob_cut, -beta, -alpha, transposition_cut, store_transposition);
        }

        public SearchParameter SwapAlphaBeta()
        {
            return new SearchParameter(depth, depth_prob_cut, -beta, -alpha, transposition_cut, store_transposition);
        }

        public SearchParameter CreateNullWindowParam()
        {
            return new SearchParameter(depth - 1, depth_prob_cut , -alpha - 1, -alpha, transposition_cut, store_transposition);
        }

        public bool ShouldObserve()
        {
            return depth_prob_cut && 0 < depth && depth < 2;
        }

        public SearchParameter ObserveDepth(Random rand)
        {
            depth_prob_cut = false;

            if (depth * 0.5 < rand.NextDouble())
                depth = 0;
            else
                depth = 2;

            return this;
        }
    }

    public class SearchParameterFactory
    {
        public readonly int stage;
        public readonly SearchType type;

        public Func<int, float> Depth { get; set; } = _ => 0;
        public Func<int, bool> ShouldTranspositionCut { get; set; } = _ => true;
        public Func<int, bool> ShouldStoreTranspositionTable { get; set; } = _ => true;

        public SearchParameterFactory(int stage, SearchType type, Func<int, float> depth)
        {
            this.stage = stage;
            this.type = type;
            Depth = depth;
        }

        public SearchParameterFactory(int stage, SearchType type, float depth) : this(stage, type, _ => depth)
        {
        }

        public SearchParameter CreateSearchParameter(int n)
        {
            float d = Depth(n);
            return SearchParameter.CreateInitParam(d, (n + d) % 2 == 1, ShouldTranspositionCut(n), ShouldStoreTranspositionTable(n));
        }
    }

    public class SearcehdOneMoveEventArg
    {
        public ulong Move { get; }
        public bool Pruned { get; }
        public float Eval { get; }
        public float Depth { get; }

        public SearcehdOneMoveEventArg(ulong move, bool pruned, float eval, float depth)
        {
            Move = move;
            Pruned = pruned;
            Eval = PlayerAI.CorrectEvaluation(eval);
            Depth = depth;
        }
    }

    public class PlayerAI : Player
    {
        public const int INF = 10000000;

        public SearchParameterFactory[] Params { get; set; }
        public Evaluator Evaluator { get; set; }

        public long SearchedNodeCount { get; set; }
        public double TakenTime { get; set; }

        public delegate void SearcehdOneMoveEventHandler(object sender, SearcehdOneMoveEventArg e);
        public event SearcehdOneMoveEventHandler SearcehdOneMoveEvent;

        public PlayerAI(Evaluator evaluator)
        {
            Evaluator = evaluator;
        }

        protected static float EvalFinishedGame(Board board)
        {
            // SearchedNodeCount++;
            return board.GetStoneCountGap() * 10000;
        }

        public float Eval(Board board)
        {
            SearchedNodeCount++;

            //if (color == 1 ^ board.n_stone % 2 == 0)
            //{
            //    return -Evaluator.Eval(board.ColorFliped());
            //}
            //else
            {
                return Evaluator.Eval(board);
            }
        }

        public static float CorrectEvaluation(float e)
        {
            if (Math.Abs(e) >= 10000)
                return e / 10000;
            else
                return e * Weight.WEIGHT_RANGE / 127.0F;
        }

        public override (int x, int y, ulong move) DecideMove(Board board, int stone)
        {
            (int x, int y, ulong move, _) = DecideMoveWithEvaluation(board, stone);
            return (x, y, move);
        }

        int color;

        public (int x, int y, ulong move, float e) DecideMoveWithEvaluation(Board board, int stone)
        {
            SearchedNodeCount = 0;

            if (stone == -1)
                board = board.ColorFliped();

            color = stone;
            Evaluator.StartSearch(stone);

            ulong result = 0;
            float e = 0;

            var timer = System.Diagnostics.Stopwatch.StartNew();

            foreach (var param in Params.OrderByDescending(p => p.stage))
            {
                if (board.n_stone < param.stage)
                    continue;

                if (param.Depth(board.n_stone) <= 1E-5)
                    e = Eval(board);
                else
                    (result, e) = SolveRoot(board, param);
                break;
            }

            timer.Stop();
            TakenTime = (double)timer.ElapsedTicks / System.Diagnostics.Stopwatch.Frequency;

            (int x, int y) = Board.ToPos(result);

            return (x, y, result, CorrectEvaluation(e));
        }

        public (ulong, float) SolveRoot(Board board, SearchParameterFactory param) => param.type switch
        {
            SearchType.Normal => SolveRoot(new Search(), board, param.CreateSearchParameter(board.n_stone)),
            SearchType.IterativeDeepening => SolveIterativeDeepening(board, param.CreateSearchParameter(board.n_stone), 2, 3),
            _ => (0, 0)
        };

        public (ulong, float) SolveIterativeDeepening(Board board, SearchParameter p, int interval, int times)
        {
            var search = new Search();
            float depth = p.depth;
            p.depth -= interval * Math.Min(times - 1, (int)Math.Ceiling((double)p.depth / interval) - 1);

            while (true)
            {
                if (PrintInfo)
                {
                    Console.WriteLine();
                    Console.WriteLine($"Depth {p.depth}");
                }

                (ulong move, float e) = SolveRoot(search, board, p);

                if (p.depth >= depth)
                    return (move, e);

                search = new SearchIterativeDeepening(search.Table, interval);
                p.depth += interval;
            }
        }

        public float Evaluate(Board board)
        {
            foreach (var param in Params.OrderByDescending(p => p.stage))
            {
                if (board.n_stone < param.stage)
                    continue;

                float e = Solve(new Search(), new Move(board), param.CreateSearchParameter(board.n_stone));
                return CorrectEvaluation(e);
            }

            return 0;
        }

        public (ulong, float) SolveRoot(Search search, Board board, SearchParameter p)
        {
            Move root = new Move(board);

            if (root.n_moves <= 1)
            {
                return (root.moves, 0);
            }

            Move[] array = root.NextMoves();
            search.OrderMoves(array, p.depth);

            Move result = array[0];
            float max = -Solve(search, array[0], p.Deepen());

            if (PrintInfo)
            {
                Console.WriteLine($"{Board.ToPos(result.move)} : {max}");
                SearcehdOneMoveEvent?.Invoke(this, new SearcehdOneMoveEventArg(result.move, false, max, p.depth));
            }

            for (int i = 1; i < array.Length; i++)
            {
                Move move = array[i];

                float eval = -Solve(search, move, p.CreateNullWindowParam());

                if (p.alpha < eval)
                {
                    p.alpha = eval;
                    eval = -Solve(search, move, p.Deepen());
                    p.alpha = Math.Max(p.alpha, eval);

                    if (PrintInfo)
                    {
                        Console.WriteLine($"{Board.ToPos(move.move)} : {eval}");
                        SearcehdOneMoveEvent?.Invoke(this, new SearcehdOneMoveEventArg(move.move, false, eval, p.depth));
                    }
                }
                else if (PrintInfo)
                {
                    Console.WriteLine($"{Board.ToPos(move.move)} : Pruned");
                    SearcehdOneMoveEvent?.Invoke(this, new SearcehdOneMoveEventArg(move.move, true, eval, p.depth));
                }

                if (max < eval)
                {
                    max = eval;
                    result = move;
                }
            }
            return (result.move, max);
        }

        public float NullWindowSearch(Search search, Move move, SearchParameter p)
        {
            return -Solve(search, move, p.CreateNullWindowParam());
        }

        public float Negascout(Search search, Board board, ulong moves, SearchParameter p)
        {
            ulong move = Board.NextMove(moves);
            moves = Board.RemoveMove(moves, move);
            float max = -Solve(search, new Move(board, move), p.Deepen());

            if (p.beta <= max)
                return max;

            p.alpha = Math.Max(p.alpha, max);

            while ((move = Board.NextMove(moves)) != 0)
            {
                moves = Board.RemoveMove(moves, move);
                Move m = new Move(board, move);

                //int eval = -Solve(search, m, param, depth - 1, -alpha - 1, -alpha);
                float eval = NullWindowSearch(search, m, p);

                if (p.beta <= eval)
                    return eval;

                if (p.alpha < eval)
                {
                    p.alpha = eval;
                    eval = -Solve(search, m, p.Deepen());

                    if (p.beta <= eval)
                        return eval;

                    p.alpha = Math.Max(p.alpha, eval);
                }
                max = Math.Max(max, eval);
            }
            return max;
        }

        public float Negascout(Search search, Move[] moves, SearchParameter p)
        {
            float max = -Solve(search, moves[0], p.Deepen());

            if (p.beta <= max)
                return max;

            p.alpha = Math.Max(p.alpha, max);

            foreach (Move move in moves.AsSpan(1, moves.Length - 1))
            {
                //int eval = -Solve(search, move, param, depth - 1, -alpha - 1, -alpha);
                float eval = NullWindowSearch(search, move, p);

                if (p.beta <= eval)
                    return eval;

                if (p.alpha < eval)
                {
                    p.alpha = eval;
                    eval = -Solve(search, move, p.Deepen());

                    // ProbcutNode1[depth]++;

                    if (p.beta <= eval)
                    {
                        // ProbcutNode2[depth]++;
                        return eval;
                    }

                    p.alpha = Math.Max(p.alpha, eval);
                }
                max = Math.Max(max, eval);
            }
            return max;
        }

        public float Negamax(Search search, Move[] moves, SearchParameter p)
        {
            float max = -1000000;

            for (int i = 0; i < moves.Length; i++)
            {
                float e = -Solve(search, moves[i], p.Deepen());
                max = Math.Max(max, e);
                p.alpha = Math.Max(p.alpha, e);

                if (p.alpha >= p.beta)
                    return max;
            }
            return max;
        }

        public float Negamax(Search search, Board board, ulong moves, SearchParameter p)
        {
            float max = -1000000;
            ulong move;
            while ((move = Board.NextMove(moves)) != 0)
            {
                moves = Board.RemoveMove(moves, move);

                float e = -Solve(search, new Move(board, move), p.Deepen());
                max = Math.Max(max, e);
                p.alpha = Math.Max(p.alpha, e);

                if (p.alpha >= p.beta)
                    return max;
            }
            return max;
        }

        public bool PrintInfo { get; set; } = true;

        public static int ordering_depth = 57;
        public static int transposition = 1;

        public virtual float Solve(Search search, Move move, SearchParameter p)
        {
            if (search.IsCanceled)
                return -1000000;

            if(p.ShouldObserve())
            {
                p = p.ObserveDepth(Program.Random);
            }

            if (p.depth <= 0)
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
                    return -Solve(search, next, p.SwapAlphaBeta());
                }
            }

            if (move.reversed.n_stone == 63 && move.n_moves == 1)
                return -EvalFinishedGame(move.reversed.Reversed(move.moves));

            if (search.TryTranspositionCutoff(move, ref p, out float lower, out float upper, ref value))
                return value;

            if (p.depth >= 3 && move.reversed.n_stone < 60)
            {
                if (move.n_moves > 3)
                    value = Negascout(search, search.OrderMoves(move.NextMoves(), p.depth), p);
                else
                    value = Negascout(search, move.reversed, move.moves, p);
            }
            else
            {
                value = Negamax(search, move.reversed, move.moves, p);
            }

            if (p.depth > transposition && move.reversed.n_stone <= ordering_depth && p.store_transposition)
                search.StoreTranspositionTable(move, p.alpha, p.beta, lower, upper, value);

            return value;
        }
    }
}
