using OthelloAI.GA;
using System;
using System.Collections.Generic;
using System.IO;
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
