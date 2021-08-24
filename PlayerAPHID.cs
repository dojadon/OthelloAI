using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace OthelloAI
{
    class WorkerTaskInfo : IComparable<WorkerTaskInfo>
    {
        public WorkerTaskInfo(int alpha, int beta)
        {
            Alpha = alpha;
            Beta = beta;
        }

        public Board Board { get; }
        public int Alpha { get; set; }
        public int Beta { get; set; }

        int IComparable<WorkerTaskInfo>.CompareTo(WorkerTaskInfo other)
        {
            return (Beta - Alpha);
        }
    }

    class TaskWorkerManager
    {
        public PlayerAphid Player { get; }
        public CutoffParameters Param { get; }
        public int WorkerDepth { get; }
        public int NumThreads { get; }
        public ConcurrentDictionary<Board, WorkerTaskInfo> BorderTable { get; } = new ConcurrentDictionary<Board, WorkerTaskInfo>();
        public ConcurrentDictionary<Board, (bool, int)> CertainValueTable { get; } = new ConcurrentDictionary<Board, (bool, int)>();
        public ConcurrentStack<Board> Keys { get; } = new ConcurrentStack<Board>();

        public SortedSet<WorkerTaskInfo> workerTasks = new SortedSet<WorkerTaskInfo>();

        public bool RunningWorkers { get; private set; }

        public TaskWorkerManager(PlayerAphid player, CutoffParameters param, int depth, int numThreads)
        {
            Player = player;
            Param = param;
            WorkerDepth = depth;
            NumThreads = numThreads;
        }

        public void AddTask(Board key, int alpha, int beta)
        {
            if (BorderTable.TryGetValue(key, out WorkerTaskInfo info))
            {
                lock (info)
                {
                    info.Alpha = Math.Max(info.Alpha, alpha);
                    info.Beta = Math.Min(info.Beta, beta);
                }
            }
            else
            {
                BorderTable[key] = new WorkerTaskInfo(alpha, beta);
                Keys.Push(key);
            }
        }

        public void Run(int id)
        {
            var tables = Enumerable.Range(0, WorkerDepth + 1).Select(i => new Dictionary<Board, (int, int)>()).ToArray();

            while (RunningWorkers)
            {
                if (!Keys.TryPop(out Board b))
                    continue;

                SolveIteractiveDeepening(tables, BorderTable[b], new Move(b), Param, WorkerDepth);
            }
        }

        private void SolveIteractiveDeepening(Dictionary<Board, (int, int)>[] tables, WorkerTaskInfo info, Move move, CutoffParameters param, int depth)
        {
            int d = depth - 4;
            Search search = new Search();

            while (true)
            {
                search.Table = tables[d];
                int e = Player.Solve(search, move, param, d, info.Alpha, info.Beta);

                if (d >= depth)
                {
                    CertainValueTable[move.reversed] = (d >= depth, e);
                    break;
                }

                d += 2;
                search = new SearchIterativeDeepening(search.Table);
            }
        }

        public void RunWorkers()
        {
            RunningWorkers = true;

            foreach (int i in Enumerable.Range(0, NumThreads))
            {
                Task.Run(() => Run(i));
            }
        }

        public void StopWorkers()
        {
            RunningWorkers = false;
        }
    }

    class SearchAphid : Search
    {
        public TaskWorkerManager Workers { get; }

        public SearchAphid(TaskWorkerManager workers)
        {
            Workers = workers;
        }
    }

    class PlayerAphid : PlayerAI
    {
        public int DepthMaser { get; } = 2;
        public int DepthWorker { get; } = 9;

        public PlayerAphid(Evaluator evaluator) : base(evaluator)
        {
        }

        public override (int x, int y, ulong move) DecideMove(Board board, int stone)
        {
            CurrentIndex = board.n_stone - 4;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            if (stone == -1)
                board = board.ColorFliped();

            ulong result;
            if (board.n_stone < ParamMid.stage)
            {
                result = SolveAphidRoot(board, ParamBeg.cutoff_param, DepthMaser);
            }
            else if (board.n_stone < ParamEnd.stage)
            {
                result = SolveAphidRoot(board, ParamMid.cutoff_param, DepthMaser);
            }
            else if (board.n_stone < ParamEnd.stage + 4)
            {
                (result, _) = SolveEndGame(new Search(), board, ParamEnd.cutoff_param);
            }
            else
            {
                (result, _) = SolveRoot(new Search(), board, ParamEnd.cutoff_param, 64);
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

        public ulong SolveAphidRoot(Board board, CutoffParameters param, int depth)
        {
            Move root = new Move(board);

            if (root.n_moves <= 1)
                return root.moves;

            TaskWorkerManager manager = new TaskWorkerManager(this, param, DepthWorker, 15);
            SearchAphid search = new SearchAphid(manager);
            manager.RunWorkers();

            Move[] moves = root.OrderedNextMoves();

            while (true)
            {
                (ulong result, bool certain) = SolveAphidRoot(search, moves, depth);

                if (certain)
                {
                    manager.StopWorkers();
                    return result;
                }
            }
        }

        public (ulong move, bool certain) SolveAphidRoot(SearchAphid search, Move[] moves, int depth)
        {
            Move result = moves[0];
            bool certain = true;
            int a1 = -INF;
            int a2 = -INF;

            for (int i = 0; i < moves.Length; i++)
            {
                Move move = moves[i];

                int eval = -SolveAphid(search, move, depth - 1, -INF, -a1, -INF, -a2, out bool isCertainNode);
                certain &= isCertainNode;

                if (a1 < eval)
                {
                    a1 = eval;
                    result = move;
                }

                if (isCertainNode)
                    a2 = Math.Max(a2, eval);
            }
            return (result.move, certain);
        }

        public int NegascoutAphid(SearchAphid search, Move[] moves, int depth, int a1, int b1, int a2, int b2, out bool certain)
        {
            certain = true;

            int max = -INF;

            foreach (Move move in moves)
            {
                int eval = -SolveAphid(search, move, depth - 1, -b1, -a1, -b2, -a2, out bool isCertainNode);
                certain &= isCertainNode;

                if (b1 <= eval)
                    return eval;

                a1 = Math.Max(a1, eval);
                max = Math.Max(max, eval);

                if (isCertainNode)
                    a2 = Math.Max(a2, eval);
            }
            return max;
        }

        public int EvalMasterLeaf(SearchAphid search, Move move, int a2, int b2, out bool certain)
        {
            if (search.Workers.CertainValueTable.TryGetValue(move.reversed, out (bool certain, int value) t))
            {
                certain = t.certain;
                return t.value;
            }
            else
            {
                certain = false;
                search.Workers.AddTask(move.reversed, a2, b2);
                return Eval(move.reversed);
            }
        }

        public int SolveAphid(SearchAphid search, Move move, int depth, int a1, int b1, int a2, int b2, out bool certain)
        {
            if (depth <= 0)
                return EvalMasterLeaf(search, move, a2, b2, out certain);

            if (move.moves == 0)
            {
                ulong opponentMoves = move.reversed.GetOpponentMoves();
                if (opponentMoves == 0)
                {
                    certain = true;
                    return EvalFinishedGame(move.reversed);
                }
                else
                {
                    Move next = new Move(move.reversed.ColorFliped(), 0, opponentMoves, Board.BitCount(opponentMoves));
                    return -SolveAphid(search, next, depth, -b1, -a1, -b2, -a2, out certain);
                }
            }

            if (move.reversed.n_stone == 63 && move.n_moves == 1)
            {
                certain = true;
                return -EvalFinishedGame(move.reversed.Reversed(move.moves));
            }

            return NegascoutAphid(search, move.OrderedNextMoves(), depth, a1, b1, a2, b2, out certain);
        }
    }
}
