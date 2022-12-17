using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OthelloAI
{
    public class TrainingData : List<TrainingDataElement>
    {
        public TrainingData()
        {
        }

        public TrainingData(IEnumerable<TrainingDataElement> data)
        {
            AddRange(data);
        }

        public void Add(Board b, int result)
        {
            Add(new TrainingDataElement(b, result));
        }

        public void Add(IEnumerable<Board> boards, float result)
        {
            AddRange(boards.Select(b => new TrainingDataElement(b, result)));
        }
    }

    public readonly struct TrainingDataElement
    {
        public readonly Board board;
        public readonly float result;

        public TrainingDataElement(Board board, float result)
        {
            this.board = board;
            this.result = result;
        }
    }

    public class TrainerUtil
    {
        public static TrainingData PlayForTrainingParallel(int n_game, Player player, int num_threads = -1)
        {
            return new TrainingData(PlayForTrainingParallelSeparated(n_game, player, num_threads).SelectMany(x => x));
        }

        public static TrainingData[] PlayForTrainingParallelSeparated(int n_game, Player player, int num_threads = -1)
        {
            return PlayForTrainingParallelSeparated(n_game, player, player, rand => Tester.CreateRandomGame(6, rand), num_threads);
        }

        public static TrainingData[] PlayForTrainingParallelSeparated(int n_game, Player player1, Player player2, Func<Random, Board> createInitBoard, int num_threads = -1)
        {
            static bool Step(ref Board board, List<Board> boards, Player player, int stone)
            {
                (int x, int y, ulong move) = player.DecideMove(board, stone);
                if (move != 0)
                {
                    board = board.Reversed(move, stone);
                    boards.Add(board);
                    return true;
                }
                return false;
            }

            int GetResult(Board b)
            {
                return b.GetStoneCountGap();
            }

            int count = 0;

            var range = Enumerable.Range(0, 16).AsParallel();

            if (num_threads > 0)
                range = range.WithDegreeOfParallelism(num_threads);

            var data = range.Select(i =>
            {
                var results = new TrainingData();
                var rand = new Random();

                while (count < n_game)
                {
                    Board board = createInitBoard(rand);
                    List<Board> boards = new List<Board>();

                    while (Step(ref board, boards, player1, 1) | Step(ref board, boards, player2, -1))
                    {
                    }
                    results.Add(boards, GetResult(board));

                    Interlocked.Increment(ref count);
                }

                return results;
            }).ToList();

            return data.Select(d => new TrainingData(d)).ToArray();
        }

        public static TrainingData PlayForTraining(int n_game, Player player, Random rand)
        {
            static bool Step(ref Board board, List<Board> boards, Player player, int stone)
            {
                (_, _, ulong move) = player.DecideMove(board, stone);
                if (move != 0)
                {
                    board = board.Reversed(move, stone);
                    boards.Add(board);
                    return true;
                }
                return false;
            }

            int GetResult(Board b)
            {
                return b.GetStoneCountGap();
            }

            var results = new TrainingData();

            for (int i = 0; i < n_game; i++)
            {
                Board board = Tester.CreateRandomGame(8, rand);
                var boards = new List<Board>();

                while (Step(ref board, boards, player, 1) | Step(ref board, boards, player, -1))
                {
                }
                results.Add(boards, GetResult(board));
            }

            return results;
        }
    }

    public class TrainingDataProvider
    {
        public void Run(PlayerAI player, int num_threads)
        {
            var queue = new ConcurrentQueue<TrainingDataElement>();

            Parallel.For(0, num_threads, _ =>
            {
                var rand = new Random();
                TrainerUtil.PlayForTraining(1, player, rand);
            });
        }
    }

    public class Trainer
    {
        public Weight Weight { get; }
        public float LearningRate { get; }

        public List<float> Log { get; } = new List<float>();

        public Trainer(Weight weights, float lr)
        {
            Weight = weights;
            LearningRate = lr;
        }

        public float Update(Board board, float result)
        {
            var boards = new RotatedAndMirroredBoards(board);
            float e = result - Weight.EvalTraining(boards);

            foreach (var b in boards)
            {
                Weight.UpdataEvaluation(b, e * LearningRate, Weight.WEIGHT_RANGE);
            }

            Log.Add(e * e);

            return e;
        }

        public static List<float> Train(Weight weight, int depth, int n_games, string path = "")
        {
            var evaluator = new EvaluatorWeightsBased(weight);
            Player player = new PlayerAI(evaluator)
            {
                Params = new[] { new SearchParameters(stage: 0, type: SearchType.Normal, depth: depth),
                                              new SearchParameters(stage: 50, type: SearchType.Normal, depth: 64)},
                PrintInfo = false,
            };

            var trainer = new Trainer(weight, 0.001F);

            for (int i = 0; i < n_games / 16; i++)
            {
                var data = TrainerUtil.PlayForTrainingParallel(16, player);
                data.ForEach(t => trainer.Update(t.board, t.result));

                Console.WriteLine($"{i} / {n_games / 16}");
                Console.WriteLine(trainer.Log.TakeLast(10000).Average());

                if (i % 100 == 0 && path != "")
                    weight.Save(path);
            }

            return trainer.Log;
        }

        public static List<int> Train(int n_games)
        {
            //ulong[] masks = new[] {
            //    0b11000011_01111110UL,
            //    0b00000001_00000111_00000111_00001110UL,
            //    0b00000001_00000001_00000001_00000011_00011110UL,
            //    0b00111100_01111110UL,
            //    0b11111111_00000000UL };

            ulong[] masks = new[] {
                0b01000010_11111111UL,
                0b00000111_00000111_00000111UL,
                0b00000001_00000001_00000001_00000011_00011111UL,
                0b11111111UL,
                0b11111111_00000000UL };

            Weight weight1 = new WeightsSum(masks.Select(m => new WeightsArrayR(m)).ToArray());
            Weight weight2 = new WeightsSum(masks.Select(m => new WeightsStagebased(Enumerable.Range(0, 4).Select(_ => new WeightsArrayR(m)).ToArray())).ToArray());

            Player player1 = new PlayerAI(new EvaluatorWeightsBased(weight1))
            {
                Params = new[] { new SearchParameters(stage: 0, type: SearchType.Normal, depth: 2),
                                              new SearchParameters(stage: 56, type: SearchType.Normal, depth: 64)},
                PrintInfo = false,
            };

            Player player2 = new PlayerAI(new EvaluatorWeightsBased(weight2))
            {
                Params = new[] { new SearchParameters(stage: 0, type: SearchType.IterativeDeepening, depth: 6),
                                              new SearchParameters(stage: 48, type: SearchType.Normal, depth: 64)},
                PrintInfo = false,
            };

            weight1.Load("e1.dat");
            weight2.Load("e2.dat");

            var trainer1 = new Trainer(weight1, 0.001F);
            var trainer2 = new Trainer(weight2, 0.001F);

            Board init = new Board(Board.InitB | 0b10000000_00000000_00000000_00000000_00000000_00000000_00000000_00000001UL, Board.InitW);
            // Board init = new Board(Board.InitB | 0b10000001_00000000_00000000_00000000_00000000_00000000_00000000_10000001UL, Board.InitW);

            var results = new List<int>();

            for (int i = 0; i < n_games / 16; i++)
            {
                var data = TrainerUtil.PlayForTrainingParallelSeparated(16, player1, player2, rand => Tester.CreateRandomGame(6, rand, init));

                foreach (var t in data.SelectMany(d => d))
                {
                    trainer1.Update(t.board, t.result);
                    trainer2.Update(t.board, t.result);
                }

                results.AddRange(data.Where(d => d.Count > 0).Select(d => d[^1].result > 0 ? 1 : 0));
                Console.WriteLine($"{results.TakeLast(10000).Average():f3}, {trainer1.Log.TakeLast(1000000).Average():f2}, {trainer2.Log.TakeLast(1000000).Average():f2}");

                if (i % 100 == 0)
                {
                    weight1.Save("e1.dat");
                    weight2.Save("e2.dat");
                }
            }

            return results;
        }
    }
}
