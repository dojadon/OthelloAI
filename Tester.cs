using System;
using System.Collections.Generic;
using System.Linq;

namespace OthelloAI
{
    class Tester
    {
        public static Board CreateRnadomGame(Random rand, int num_moves)
        {
            Board Step(Board b)
            {
                Move[] moves = new Move(b).NextMoves();
                Move move = moves[rand.Next(moves.Length)];
                return move.reversed;
            }

            Board board = Board.Init;

            for (int i = 0; i < num_moves; i++)
            {
                board = Step(board);
            }

            return board;
        }

        public static Board PlayGame(Board init, Player p1, Player p2)
        {
            static bool Step(ref Board board, Player player, int stone)
            {
                (_, _, ulong move) = player.DecideMove(board, stone);
                if (move != 0)
                {
                    board = board.Reversed(move, stone);
                    return true;
                }
                return false;
            }

            Board board = init;
            while (Step(ref board, p1, 1) | Step(ref board, p2, -1))
            {
            }
            return board;
        }

        public static void TestB()
        {
            Pattern[] patterns = Program.PATTERNS;

            foreach (var p in patterns)
                p.Load();

            int Play(PlayerAI p1, PlayerAI p2)
            {
                bool Step(ref Board board, Player player, int stone)
                {
                    (int x, int y, ulong move) = player.DecideMove(board, stone);

                    if (move != 0)
                    {
                        board = board.Reversed(move, stone);
                        return true;
                    }
                    return false;
                }

                Board board = Tester.CreateRnadomGame(GA.GATest.Random, 8);
                while (Step(ref board, p1, 1) | Step(ref board, p2, -1))
                {
                }

                return board.GetStoneCountGap();
            }

            int n = 8;

            for (int i = 1; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    PlayerAI p1 = new PlayerAI(new EvaluatorPatternBased(patterns))
                    {
                        ParamBeg = new SearchParameters(depth: i, stage: 0, new CutoffParameters(true, true, false)),
                        ParamMid = new SearchParameters(depth: i, stage: 16, new CutoffParameters(true, true, false)),
                        ParamEnd = new SearchParameters(depth: 64, stage: 48, new CutoffParameters(true, true, false)),
                        PrintInfo = false,
                    };

                    // p1.Depth_Prob = i * 0.2F;

                    PlayerAI p2 = new PlayerAI(new EvaluatorPatternBased(patterns))
                    {
                        ParamBeg = new SearchParameters(depth: j, stage: 0, new CutoffParameters(true, true, false)),
                        ParamMid = new SearchParameters(depth: j, stage: 16, new CutoffParameters(true, true, false)),
                        ParamEnd = new SearchParameters(depth: 64, stage: 48, new CutoffParameters(true, true, false)),
                        PrintInfo = false,
                    };

                    // p2.Depth_Prob = j * 0.2F;

                    double avg = Enumerable.Range(0, 500).AsParallel().Select(i =>
                    {
                        return Play(p1, p2) switch
                        {
                            <0 => 0,
                            >0 => 1,
                            0 => 0.5
                        };
                    }).Average();

                    Console.WriteLine($"{i}, {j}, {avg}");
                }
            }
        }

        public static void TestA()
        {
            Pattern[] patterns = Program.PATTERNS;

            foreach (var p in patterns)
                p.Load();

            int Play(PlayerAI p1, PlayerAI p2, List<long> count)
            {
                bool Step(ref Board board, Player player, int stone)
                {
                    if (board.n_stone >= 50)
                        return false;

                    (int x, int y, ulong move) = player.DecideMove(board, stone);
                    count.Add(p1.SearchedNodeCount);

                    if (move != 0)
                    {
                        board = board.Reversed(move, stone);
                        return true;
                    }
                    return false;
                }

                Board board = Tester.CreateRnadomGame(GA.GATest.Random, 8);
                while (Step(ref board, p1, 1) | Step(ref board, p2, -1))
                {
                }

                return board.GetStoneCountGap();
            }

            for (int i = 0; i < 11; i++)
            {
                PlayerAI player = new PlayerAI(new EvaluatorPatternBased(patterns))
                {
                    ParamBeg = new SearchParameters(depth: 3, stage: 0, new CutoffParameters(true, true, false)),
                    ParamMid = new SearchParameters(depth: 3, stage: 16, new CutoffParameters(true, true, false)),
                    ParamEnd = new SearchParameters(depth: 64, stage: 48, new CutoffParameters(true, true, false)),
                    PrintInfo = false,
                };

                player.Depth_Prob = i * 0.1F;

                double avg = Enumerable.Range(0, 1000).AsParallel().SelectMany(i =>
                {
                    List<long> c = new();
                    Play(player, player, c);
                    return c;
                }).Average();

                Console.WriteLine($"{i}, {avg}");
            }
        }

        public static void TestC()
        {
            var rand = new Random();
            Pattern[] patterns = Program.PATTERNS;

            foreach (var p in patterns)
                p.Load();

            (IEnumerable<float>, IEnumerable<float>) Play(PlayerAI p1, PlayerAI p2)
            {
                bool Step(ref Board board, PlayerAI player, int stone, List<float> evaluations)
                {
                    (int x, int y, ulong move, float e) = player.DecideMoveWithEvaluation(board, stone);

                    if (move != 0)
                    {
                        board = board.Reversed(move, stone);
                        evaluations.Add(e);
                        return true;
                    }
                    return false;
                }

                Board board = Board.Init;
                List<float> e1 = new List<float>();
                List<float> e2 = new List<float>();

                while (e1.Count < 10 || e2.Count < 10)
                {
                    e1 = new List<float>();
                    e2 = new List<float>();

                    board = Tester.CreateRnadomGame(GA.GATest.Random, 8);
                    while (Step(ref board, p1, 1, e1) | Step(ref board, p2, -1, e2))
                    {
                    }
                }

                int result = board.GetStoneCountGap();
                var error1 = e1.Select(e => (e - result) * (e - result));
                var error2 = e2.Select(e => (e - result) * (e - result));

                return (error1, error2);
            }

            for (int i = 1; i < 10; i++)
            {
                PlayerAI player = new PlayerAI(new EvaluatorPatternBased(patterns))
                {
                    ParamBeg = new SearchParameters(depth: i, stage: 0, new CutoffParameters(true, true, false)),
                    ParamMid = new SearchParameters(depth: i, stage: 16, new CutoffParameters(true, true, false)),
                    ParamEnd = new SearchParameters(depth: 64, stage: 54 - i, new CutoffParameters(true, true, false)),
                    PrintInfo = false,
                };

                var e = Enumerable.Range(0, 100).AsParallel().Select(i => Play(player, player)).ToArray();
                (float a1, float v1) = e.SelectMany(t => t.Item1).OrderByDescending(a => a).Skip(200).AverageAndVariance();
                (float a2, float v2) = e.SelectMany(t => t.Item2).OrderByDescending(a => a).Skip(200).AverageAndVariance();

                Console.WriteLine($"{i}, {a1}, {a2}");
            }
        }

        public static void TestE()
        {
            var rand = new Random();
            var timer = new System.Diagnostics.Stopwatch();
            int n = (int)1E+7;

            BoardHasher CreateRandomHasher(int n)
            {
                // return new BoardHasherMask(rand.GenerateRegion(24, n));
                return new BoardHasherScanning(new BoardHasherMask(rand.GenerateRegion(24, n)).Positions);
            }

            for (int size = 2; size < 11; size++)
            {
                var patterns = Enumerable.Range(0, 100).Select(_ => Pattern.Create(CreateRandomHasher(size), 1, PatternType.ASYMMETRIC)).ToArray();
                timer.Reset();

                for (int i = 0; i < n; i++)
                {
                    var p = rand.Choice(patterns);
                    var b = new RotatedAndMirroredBoards(rand.NextBoard());

                    timer.Start();
                    p.Eval(b);
                    timer.Stop();
                }

                var time_s = (double)timer.ElapsedTicks / System.Diagnostics.Stopwatch.Frequency / n;
                var time_nano = time_s * 1E+9;
                Console.WriteLine($"{size}, {time_nano}");
            }
        }

        //public static void Test1()
        //{
        //    var rand = new Random();

        //    static ulong Decode(float[] keys)
        //    {
        //        var indices = keys.Select((k, i) => (k, i)).OrderBy(t => t.k).Select(t => t.i).Take(6);

        //        ulong g = 0;
        //        foreach (var i in indices)
        //            g |= 1UL << i;
        //        return g;
        //    }

        //    var io = new IndividualIO<float[]>()
        //    {
        //        Decoder = Decode,
        //        ReadGenome = reader =>
        //        {
        //            var gene = new float[reader.ReadInt32()];
        //            for (int i = 0; i < gene.Length; i++)
        //                gene[i] = reader.ReadSingle();
        //            return gene;
        //        },
        //        WriteGenome = (gene, writer) =>
        //        {
        //            writer.Write(gene.Length);
        //            Array.ForEach(gene, writer.Write);
        //        }
        //    };

        //    var generator = new GenomeInfo<float[]>()
        //    {
        //        NumTuple = 4,
        //        SizeTuple = 6,
        //        GenomeGenerator = i => Enumerable.Range(0, 19).Select(_ => (float)rand.NextDouble()).ToArray(),
        //        Decoder = Decode,
        //    };

        //    // var pop = Enumerable.Range(0, 50).Select(_ => generator.Generate()).ToList();
        //    // io.Save("test/pop.dat", pop);

        //    var pop = io.Load("test/pop.dat");

        //    var file = $"test/selfplay_(100)_tournament_1ply_{DateTime.Now:yyyy_MM_dd_HH_mm}.csv";
        //    using StreamWriter sw = File.AppendText(file);

        //    var rankingLog = new List<List<int>>();

        //    for (int i = 0; i < 25; i++)
        //    {
        //        foreach (var ind in pop)
        //        {
        //            ind.Log.Clear();

        //            foreach (var p in ind.Patterns)
        //                p.Reset();
        //        }

        //        // var trainer = new PopulationTrainerCoLearning(1, 54, 1000, true);
        //        var trainer = new PopulationTrainerSelfPlay(1, 54, 100, true);

        //        // var popEvaluator = new PopulationEvaluatorRandomTournament<float[]>(trainer, 1, 54, 6500);
        //        var popEvaluator = new PopulationEvaluatorTournament<float[]>(trainer, 1, 54);
        //        var scores = popEvaluator.Evaluate(pop).Select(s => s.score);

        //        var ranking = scores.Ranking().ToList();
        //        rankingLog.Add(ranking);

        //        sw.WriteLine(string.Join(", ", scores) + ",," + string.Join(", ", ranking));

        //        Console.WriteLine(i);
        //    }

        //    var avg = Enumerable.Range(0, pop.Count).Select(i => rankingLog.Select(rank => rank[i]).Average());
        //    var var = Enumerable.Range(0, pop.Count).Select(i => rankingLog.Select(rank => rank[i]).AverageAndVariance().var);

        //    sw.WriteLine();
        //    sw.WriteLine(string.Join(", ", avg));
        //    sw.WriteLine(string.Join(", ", var));
        //    sw.WriteLine();
        //    sw.WriteLine(var.Average());

        //    // var popEvaluator = new PopulationEvaluatorTournament(new PopulationEvaluatorSelfPlay(1, 54, n_games, true), 1, 54);
        //    // var popEvaluator = new PopulationEvaluatorCoLearning(7, 52, n_games);
        //    // var popEvaluator = new PopulationEvaluatorSelfPlay(1, 52, n_games, true);
        //    // popEvaluator.Evaluate(pop);

        //    // Console.WriteLine(string.Join(", ", pop.Select(ind => ind.Score)));
        //    //using StreamWriter sw = File.AppendText(file);

        //    //for (int i = 0; i < pop[0].Log.Count - n_mov; i++)
        //    //{
        //    //    sw.WriteLine(string.Join(", ", pop.Select(ind => ind.Log.Skip(i).Take(n_mov).Average())));
        //    //}
        //}
    }
}
