using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace OthelloAI
{
    enum TaskStat
    {
        Waiting, Running, Finished
    }

    class WorkerTaskInfo
    {
        public WorkerTaskInfo(float alpha, float beta)
        {
            Alpha = alpha;
            Beta = beta;
        }

        public int ThreadId { get; set; } = -1;
        public TaskStat Stat { get; set; } = TaskStat.Waiting;
        public float Alpha { get; set; }
        public float Beta { get; set; }
    }

    class SearchAphidWorker : Search
    {
        public int Id { get; set; }
        public Board Board { get; set; }
        public ConcurrentStack<(Board, float, float)> Messages { get; } = new ConcurrentStack<(Board, float, float)>();

        public float Alpha { get; set; } = -1000000;
        public float Beta { get; set; } = 1000000;

        public void AddMessage(Board key, float alpha, float beta)
        {
            Messages.Push((key, alpha, beta));
        }

        public void UpdateBorder()
        {
            while (Messages.TryPop(out (Board key, float alpha, float beta) t))
            {
                if (Board != t.key)
                {
                    //Console.WriteLine($"{Id} : [{t.alpha}, {t.beta}] Key doesn't match");
                    continue;
                }

                //Console.WriteLine($"{t.key}\n{Id} : [{t.alpha}, {t.beta}]");

                Alpha = Math.Max(Alpha, t.alpha);
                Beta = Math.Min(Beta, t.beta);
            }
        }

        public override bool TryCutoffOrUpdateBorder(Move move, CutoffParameters param, int depth, ref float alpha, ref float beta, ref float value)
        {
            if (depth <= 4)
                return false;

            UpdateBorder();

            (float lower, float upper) = IsPlayer ? (Alpha, Beta) : (-Beta, -Alpha);

            //Console.WriteLine($"{move.reversed}\n{IsPlayer}, {depth} : [{lower}, {upper}]");

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
    }

    class TaskWorkers
    {
        public PlayerAphid Player { get; }
        public CutoffParameters Param { get; }
        public int WorkerDepth { get; }
        public int NumThreads { get; }
        public ConcurrentDictionary<Board, WorkerTaskInfo> BorderTable { get; } = new ConcurrentDictionary<Board, WorkerTaskInfo>();
        public ConcurrentDictionary<Board, float> CertainValueTable { get; } = new ConcurrentDictionary<Board, float>();
        public ConcurrentStack<Board> Keys { get; } = new ConcurrentStack<Board>();

        public SearchAphidWorker[] Searches { get; }

        public bool RunningWorkers { get; private set; }

        public TaskWorkers(PlayerAphid player, CutoffParameters param, int depth, int numThreads)
        {
            Player = player;
            Param = param;
            WorkerDepth = depth;
            NumThreads = numThreads;

            Searches = new SearchAphidWorker[numThreads];
        }

        public void AddTask(Board key, float alpha, float beta)
        {
            if (BorderTable.TryGetValue(key, out WorkerTaskInfo info) && info.Stat != TaskStat.Finished)
            {
                float a_tmp = Math.Max(info.Alpha, alpha);
                float b_tmp = Math.Min(info.Beta, beta);

                if (a_tmp != info.Alpha || b_tmp != info.Beta)
                {
                    info.Alpha = a_tmp;
                    info.Beta = b_tmp;

                    if (info.Stat == TaskStat.Running)
                        Searches[info.ThreadId].AddMessage(key, info.Alpha, info.Beta);
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
            var search = new SearchAphidWorker() { Id = id };
            Searches[id] = search;

            while (RunningWorkers)
            {
                if (!Keys.TryPop(out Board b))
                    continue;

                //Console.WriteLine($"{id} : Recieved");

                search.Board = b;
                search.Reset();

                var info = BorderTable[b];
                info.ThreadId = id;
                info.Stat = TaskStat.Running;

                float al = info.Alpha;
                float be = info.Beta;
                float e1 = Player.Solve(search, new Move(b), Param, WorkerDepth, al, be);
                float e2 = Player.Solve(new Search(), new Move(b), Param, WorkerDepth, al, be);
                Console.WriteLine($"{e1}, {e2}, [{info.Alpha}, {info.Beta}], [{al}, {be}]");
                CertainValueTable[b] = e2;

                info.Stat = TaskStat.Finished;

                //Console.WriteLine($"{id} : Finished");
            }
        }

        public void RunWorkers()
        {
            RunningWorkers = true;

            for(int i = 0;i < NumThreads; i++)
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
        public TaskWorkers Workers { get; }

        public SearchAphid(TaskWorkers workers)
        {
            Workers = workers;
        }
    }

    class PlayerAphid : PlayerAI
    {
        public int DepthMaser { get; } = 3;
        public int DepthWorker { get; } = 7;

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

            TaskWorkers workers = new TaskWorkers(this, param, DepthWorker, 15);
            workers.RunWorkers();

            Move[] moves = root.OrderedNextMoves();

            SearchAphid search = new SearchAphid(workers);
            ulong result = 0;
            bool certain = false;

            while (!certain)
            {
                (result, certain) = SolveAphidRoot(search, moves, depth);
                // Console.WriteLine($"Move : {Board.ToPos(result)}");
            }
            workers.StopWorkers();

            return result;
        }

        public (ulong move, bool certain) SolveAphidRoot(SearchAphid search, Move[] moves, int depth)
        {
            Move result = moves[0];
            bool certain = true;
            float a1 = -INF;
            float a2 = -INF;

            for (int i = 0; i < moves.Length; i++)
            {
                Move move = moves[i];

                float eval = -SolveAphid(search, move, depth - 1, -INF, -a1, -INF, -a2, out bool isCertainNode);
                certain &= isCertainNode;

                // if (isCertainNode)
                //Console.WriteLine($"{Board.ToPos(move.move)} : {eval}");

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

        public float NegascoutAphid(SearchAphid search, Move[] moves, int depth, float a1, float b1, float a2, float b2, out bool certain)
        {
            certain = true;

            foreach (Move move in moves)
            {
                float eval = -SolveAphid(search, move, depth - 1, -b1, -a1, -b2, -a2, out bool isCertainNode);
                certain &= isCertainNode;

                if (b1 <= eval)
                    return eval;

                a1 = Math.Max(a1, eval);

                if (isCertainNode)
                    a2 = Math.Max(a2, eval);
            }
            return a1;
        }

        public float EvalMasterLeaf(SearchAphid search, Move move, float a2, float b2, out bool certain)
        {
            if (search.Workers.CertainValueTable.TryGetValue(move.reversed, out float value))
            {
                certain = true;
                return value;
            }
            else
            {
                certain = false;
                search.Workers.AddTask(move.reversed, a2, b2);
                return Eval(move.reversed);
            }
        }

        public float SolveAphid(SearchAphid search, Move move, int depth, float a1, float b1, float a2, float b2, out bool certain)
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
