using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using OthelloAI.Patterns;
using OthelloAI.GA;
using System.Threading.Tasks;
using System.IO;

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

            for(int i = 0; i < num_moves; i++)
            {
                board = Step(board);
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

            // var pop = Enumerable.Range(0, 10).Select(_ => new Individual(Enumerable.Range(0, 4).Select(_ => rand.GenerateRegion(19, 6)).ToArray())).ToList();
            // Save(pop, "test/pop.dat");

            var pop = Load("test/pop.dat");

            //var pop = new List<Individual>
            //{
            //    new Individual(new ulong[] { 0b10100101_11110000UL, 0b1000001_1010001_10000011UL, 0b100000_10101000_00111100UL, 0b10000000_11101111UL }),
            //    new Individual(new ulong[] { 0b11111111UL, 0b11000000_11100000_11100000UL, 0b10000000_11100000_11110000UL, 0x8040201008040201UL })
            //};

            var evaluator = new EvaluatorRandomChoice(pop.Select(i => i.CreateEvaluator()).ToArray());

            PlayerAI player = new PlayerAI(evaluator)
            {
                ParamBeg = new SearchParametersSimple(depth: 3, stage: 0, new CutoffParameters(true, true, false)),
                ParamMid = new SearchParametersSimple(depth: 3, stage: 16, new CutoffParameters(true, true, false)),
                ParamEnd = new SearchParametersSimple(depth: 64, stage: 52, new CutoffParameters(true, true, false)),
                PrintInfo = false,
            };

            var file = $"test/{DateTime.Now:yyyy_MM_dd_HH_mm}.csv";

            using StreamWriter sw = File.AppendText(file);

            void Print(string line)
            {
                sw.WriteLine(line);
                Console.WriteLine(line);
            }

            for (int i = 0; i < 500; i++)
            {
                var data = TrainerUtil.PlayForTrainingParallel(16, player);

                Parallel.ForEach(pop, ind =>
                {
                    float e = data.Select(t => ind.Trainer.Update(t.board, t.result)).Select(f => f * f).Average();
                    ind.Log.Add(e);
                });

                Print(string.Join(", ", pop.Select(i => i.Log.TakeLast(200).Average())));
            }

            var test = Enumerable.Range(2001, 15).SelectMany(i => RecordReader.CreateTrainingData(new WthorRecordReader($"WTH/WTH_{i}.wtb")));
            var error = pop.Select(ind => test.Select(d => ind.Trainer.Test(d.board, d.result)).Select(Math.Abs).Average());
            Print(string.Join(", ", error));

            // 16.165823, 16.374949
            // 16.213587, 16.305311
        }
    }
}
