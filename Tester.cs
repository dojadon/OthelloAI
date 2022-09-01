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

        public static void Save(List<Individual> pop, string file)
        {
            using var writer = new BinaryWriter(new FileStream(file, FileMode.Create));

            writer.Write(pop.Count);

            foreach (var ind in pop)
            {
                writer.Write(ind.Genome.Length);
                Array.ForEach(ind.Genome, writer.Write);
            }
        }

        public static List<Individual> Load(string file)
        {
            using var reader = new BinaryReader(new FileStream(file, FileMode.Open));

            int n = reader.ReadInt32();

            var pop = new List<Individual>();

            for (int i = 0; i < n; i++)
            {
                int n_pattern = reader.ReadInt32();
                var ind = new Individual(Enumerable.Range(0, n_pattern).Select(_ => reader.ReadUInt64()).ToArray());
                pop.Add(ind);
            }
            return pop;
        }

        public static void Test1()
        {
            var rand = new Random();

            // var pop = Enumerable.Range(0, 50).Select(_ => new Individual(Enumerable.Range(0, 4).Select(_ => rand.GenerateRegion(19, 6)).ToArray())).ToList();
            // Save(pop, "test/pop.dat");

            var pop = Load("test/pop.dat");

            //var pop = new List<Individual>
            //{
            //    new Individual(new ulong[] { 0b10100101_11110000UL, 0b1000001_1010001_10000011UL, 0b100000_10101000_00111100UL, 0b10000000_11101111UL }),
            //    new Individual(new ulong[] { 0b11111111UL, 0b11000000_11100000_11100000UL, 0b10000000_11100000_11110000UL, 0x8040201008040201UL })
            //};

            foreach (var ind in pop)
                foreach (var p in ind.Patterns)
                {
                    // p.Load();
                }

            int n_games = 1000;
            int n_mov = 500;

            var file = $"test/co_learning_7ply_{DateTime.Now:yyyy_MM_dd_HH_mm}.csv";
            using StreamWriter sw = File.AppendText(file);

            var rankingLog = new List<List<int>>();

            for (int i = 0; i < 25; i++)
            {
                foreach (var ind in pop)
                {
                    ind.Log.Clear();

                    foreach (var p in ind.Patterns)
                        p.Reset();
                }

                var trainer = new PopulationEvaluatorCoLearning(7, 50, n_games);
                trainer.Train(pop);

                //var popEvaluator = new PopulationEvaluatorTournament(3, 54);
                //popEvaluator.Evaluate(pop);

                var ranking = pop.Select(ind => ind.Score).Ranking().ToList();
                rankingLog.Add(ranking);

                sw.WriteLine(string.Join(", ", pop.Select(ind => ind.Score)) + ",," + string.Join(", ", ranking));

                Console.WriteLine(i);
            }

            sw.WriteLine();
            sw.WriteLine(string.Join(", ", Enumerable.Range(0, pop.Count).Select(i => rankingLog.Select(rank => rank[i]).Average())));
            sw.WriteLine(string.Join(", ", Enumerable.Range(0, pop.Count).Select(i => rankingLog.Select(rank => rank[i]).AverageAndVariance().var)));

            // var popEvaluator = new PopulationEvaluatorTournament(new PopulationEvaluatorSelfPlay(1, 54, n_games, true), 1, 54);
            // var popEvaluator = new PopulationEvaluatorCoLearning(7, 52, n_games);
            // var popEvaluator = new PopulationEvaluatorSelfPlay(1, 52, n_games, true);
            // popEvaluator.Evaluate(pop);

            // Console.WriteLine(string.Join(", ", pop.Select(ind => ind.Score)));
            //using StreamWriter sw = File.AppendText(file);

            //for (int i = 0; i < pop[0].Log.Count - n_mov; i++)
            //{
            //    sw.WriteLine(string.Join(", ", pop.Select(ind => ind.Log.Skip(i).Take(n_mov).Average())));
            //}
        }
    }
}
