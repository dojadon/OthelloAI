using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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

        public virtual bool TryProbCutoff(PlayerAI player, Move move, SearchParameter p, int depthShallow, float avg, float var, ref float value)
        {
            float low = p.alpha - avg - var * 1.6F;

            if (player.Solve(this, move, new SearchParameter(depthShallow, low - 1, low, false, false)) < low)
            {
                value = p.alpha - 1;
                return true;
            }

            float up = p.beta - avg + var * 1.6F;

            if (player.Solve(this, move, new SearchParameter(depthShallow, up, up + 1, false, false)) > up)
            {
                value = p.beta + 1;
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
        public float alpha, beta;
        public readonly bool transposition_cut, store_transposition;

        public SearchParameter(float depth, float alpha, float beta, bool transposition_cut, bool store_transposition)
        {
            this.depth = depth;
            this.alpha = alpha;
            this.beta = beta;
            this.transposition_cut = transposition_cut;
            this.store_transposition = store_transposition;
        }

        public static SearchParameter CreateInitParam(float depth, bool transposition_cut, bool store_transposition)
        {
            return new SearchParameter(depth, -PlayerAI.INF, PlayerAI.INF, transposition_cut, store_transposition);
        }

        public SearchParameter Deepen()
        {
            return new SearchParameter(depth - 1, -beta, -alpha, transposition_cut, store_transposition);
        }

        public SearchParameter Deepen(int d)
        {
            if (d % 2 == 0)
                return new SearchParameter(depth - d, alpha, beta, transposition_cut, store_transposition);
            else
                return new SearchParameter(depth - d, -beta, -alpha, transposition_cut, store_transposition);
        }

        public SearchParameter SwapAlphaBeta()
        {
            return new SearchParameter(depth, -beta, -alpha, transposition_cut, store_transposition);
        }

        public SearchParameter CreateNullWindowParam()
        {
            return new SearchParameter(depth - 1, -alpha - 1, -alpha, transposition_cut, store_transposition);
        }

        public bool ShouldObserve(Board b)
        {
            return 0 < depth && depth < 2 && (b.n_stone % 2 == 0);
        }

        public SearchParameter ObserveDepth(Random rand)
        {
            float p = depth * 0.5F;

            if (p < rand.NextDouble())
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
            return SearchParameter.CreateInitParam(d, ShouldTranspositionCut(n), ShouldStoreTranspositionTable(n));
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

        public Random Random { get; init; } = new Random();

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

            //if (board.n_stone % 2 != 0)
            //{
            //    Console.WriteLine(board.n_stone);
            //}

            if ((board.n_stone & 1) != 0)
            {
                return -Evaluator.Eval(board.ColorFliped());
            }
            else
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

        public Book Book { get; set; }

        int color;

        public (int x, int y, ulong move, float e) DecideMoveWithEvaluation(Board board, int stone)
        {
            SearchedNodeCount = 0;

            if (Book != null)
            {
                ulong move = Book.Search(board, stone);

                if (move != 0)
                {
                    Console.WriteLine("Found Position");

                    (int xx, int yy) = Board.ToPos(move);

                    return (xx, yy, move, 0);
                }
            }

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

        public List<float>[] Errors = 60.Loop(_ => new List<float>()).ToArray();

        public static double[] avg = {
            -0.040797614,
0.66978055    ,
0.37934193    ,
0.86878          ,
0.5464785      ,
2.8221052      ,
0.29154423    ,
3.66192          ,
0.7397154      ,
4.0271688      ,
0.9654541      ,
4.3736925      ,
1.2865216      ,
4.479592        ,
1.555555        ,
4.2833447      ,
2.0407941      ,
4.520586        ,
1.9052997      ,
4.4924097      ,
0.95056          ,
5.4000936      ,
0.4569389      ,
4.5676417      ,
3.7433705      ,
4.3786507      ,
4.383622        ,
4.59787          ,
4.6662345      ,
4.741156        ,
3.7971044      ,
3.5280662      ,
4.9263673      ,
4.03094          ,
5.949238        ,
5.3838835      ,
5.390336        ,
5.227772        ,
5.5430946      ,
4.0768523      ,
4.712555        ,
5.01673          ,
7.732921        ,
3.4486496      ,
6.312923        ,
0.04794008    ,
6.525366        ,
-0.054144207,
4.536696        ,
0.51772803    ,
4.403394        ,
0.029048447  ,
3.1076183      ,
-0.37305945  ,
1.2830694      ,
-0.34651178  ,
-0.032439914,
};

        public static double[] var =
        {
            0.903211564,
1.148147494,
2.468252397,
1.179641874,
3.312044076,
2.851501297,
3.279486627,
3.767106141,
3.596443825,
3.951706221,
3.700687556,
3.989085087,
3.952979406,
4.203443551,
3.614856076,
4.481601828,
3.84079151  ,
4.210410232,
3.853231753,
4.206731787,
4.391371043,
5.946572851,
4.552847908,
5.341285182,
5.60257932  ,
5.591765323,
6.239090821,
5.827181088,
6.805832706,
5.816464948,
7.34040763  ,
6.86643365  ,
7.556012358,
7.61525341  ,
7.776866981,
7.855476763,
8.247550832,
8.10072103  ,
7.754332039,
7.62969351  ,
7.581629998,
7.920663353,
11.63516414,
10.68029828,
10.74463431,
9.755358666,
9.619264157,
9.04140274  ,
9.088559482,
8.046162931,
8.180113278,
7.665379154,
7.034635958,
5.962356099,
6.615256864,
5.800548449,
4.944035321,

        };

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

            // float eval_4 = -Solve(search, array[0], p.Deepen(5));
            // Errors[board.n_stone - 4].Add(CorrectEvaluation(max) - CorrectEvaluation(eval_4));

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
                    // eval_4 = -Solve(search, move, SearchParameter.CreateInitParam(2, false, false));
                    // Errors[board.n_stone - 4].Add(CorrectEvaluation(eval) - CorrectEvaluation(eval_4));

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

            if (p.ShouldObserve(move.reversed))
            {
                p = p.ObserveDepth(Random);
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

            if (p.depth == 6 &&  search.TryProbCutoff(this, move, p, 2, (float) avg[move.reversed.n_stone - 4], (float) var[move.reversed.n_stone - 4], ref value))
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