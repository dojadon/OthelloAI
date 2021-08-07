using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OthelloAI
{
    class TaskWorkers
    {
        public PlayerAPHID Player { get; }
        public CutoffParameters Param { get; }
        public int WorkerDepth { get; }
        public ConcurrentDictionary<Board, (float, float)> BorderTable { get; } = new ConcurrentDictionary<Board, (float, float)>();
        public ConcurrentDictionary<Board, float> CertainValueTable { get; } = new ConcurrentDictionary<Board, float>();
        public ConcurrentStack<Board> Keys { get; } = new ConcurrentStack<Board>();

        public Task[] Tasks { get; private set; }

        bool RunningWorkers { get; set; }

        public TaskWorkers(PlayerAPHID player, CutoffParameters param, int depth)
        {
            Player = player;
            Param = param;
            WorkerDepth = depth;
        }

        public void AddTask(Board key, float alpha, float beta)
        {
            if (BorderTable.TryGetValue(key, out (float a, float b) t))
            {
                BorderTable[key] = (Math.Max(t.a, alpha), Math.Min(t.b, beta));
            }
            else
            {
                BorderTable[key] = (alpha, beta);
                Keys.Push(key);
            }
        }

        public void RunWorkers(int n)
        {
            RunningWorkers = true;
            Tasks = Enumerable.Range(0, n).Select(i => Task.Run(Run)).ToArray();
        }

        public void StopWorkers()
        {
            RunningWorkers = false;
        }

        public void Run()
        {
            while (RunningWorkers)
            {
                if (!Keys.TryPop(out Board b))
                    continue;

                (float alpha, float beta) = BorderTable[b];
                CertainValueTable[b] = Player.Solve(new Search(), new Move(b), Param, WorkerDepth, alpha, beta);
            }
        }
    }

    class SearchAPHID : Search
    {
        public TaskWorkers Workers { get; }

        public SearchAPHID(TaskWorkers workers)
        {
            Workers = workers;
        }
    }

    class PlayerAPHID : PlayerAI
    {
        public int DepthMaser { get; }

        public PlayerAPHID(Evaluator evaluator) : base(evaluator)
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
                result = SolveAphidRoot(board, ParamBeg.cutoff_param, 3);
            }
            else if (board.n_stone < ParamEnd.stage)
            {
                result = SolveAphidRoot(board, ParamMid.cutoff_param, 3);
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
            TaskWorkers workers = new TaskWorkers(this, param, 7);
            workers.RunWorkers(15);

            Move root = new Move(board);

            if (root.n_moves <= 1)
                return root.moves;

            Move[] moves = root.OrderedNextMoves();

            SearchAPHID search = new SearchAPHID(workers);
            ulong result = 0;
            bool certain = false;

            while (!certain)
            {
                (result, certain) = SolveAphidRoot(search, moves, depth);
            }
            workers.StopWorkers();

            return result;
        }

        public (ulong move, bool certain) SolveAphidRoot(SearchAPHID search, Move[] moves, int depth)
        {
            Move result = moves[0];
            float alpha = -SolveAphid(search, moves[0], depth - 1, -INF, INF, -INF, INF, out bool certain);
            float a2 = certain ? alpha : -INF;

         //   if (PrintInfo)
            //    Console.WriteLine($"{Board.ToPos(result.move)} : {alpha}");

            for (int i = 1; i < moves.Length; i++)
            {
                Move move = moves[i];

                float eval = -SolveAphid(search, move, depth - 1, -1000000, -alpha, -INF, INF, out bool isCertainNode);
                certain &= isCertainNode;

              //  if (PrintInfo)
                 //   Console.WriteLine($"{Board.ToPos(move.move)} : {eval}");

                if (alpha < eval)
                {
                    alpha = eval;
                    result = move;
                }

                if (isCertainNode)
                    a2 = Math.Max(a2, eval);
            }
            return (result.move, certain);
        }

        public float NegascoutAphid(SearchAPHID search, Move[] moves, int depth, float a1, float b1, float a2, float b2, out bool certain)
        {
            float max = -SolveAphid(search, moves[0], depth - 1, -b1, -a1, -b2, -a2, out bool isCertainNode);
            certain = isCertainNode;

            if (b1 <= max)
                return max;

            a1 = Math.Max(a1, max);
            if (isCertainNode)
                a2 = Math.Max(a2, max);

            foreach (Move move in moves.AsSpan(1, moves.Length - 1))
            {
                float eval = -SolveAphid(search, move, depth - 1, -a1 - 1, -a1, -b2, -a2, out isCertainNode);
                certain &= isCertainNode;

                if (b1 <= eval)
                    return eval;

                a1 = Math.Max(a1, eval);
                max = Math.Max(max, eval);

                if (isCertainNode)
                    a2 = Math.Max(a2, max);
            }
            return max;
        }

        int ordering_depth = 57;

        public float EvalMasterLeaf(SearchAPHID search, Move move, float a2, float b2, out bool certain)
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

        public float SolveAphid(SearchAPHID search, Move move, int depth, float a1, float b1, float a2, float b2, out bool certain)
        {
            certain = true;

            if (search.IsCanceled)
                return -1000000;

            if (depth <= 0)
                return EvalMasterLeaf(search, move, a2, b2, out certain);

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
                    return -SolveAphid(search, next, depth, -b1, -a1, -b2, -a2, out certain);
                }
            }

            if (move.reversed.n_stone == 63 && move.n_moves == 1)
                return -EvalFinishedGame(move.reversed.Reversed(move.moves));

            return NegascoutAphid(search, move.OrderedNextMoves(), depth, a1, b1, a2, b2, out certain);
        }
    }
}
