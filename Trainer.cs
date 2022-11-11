using System;
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

        public TrainingData(List<TrainingDataElement> data)
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

    public struct TrainingDataElement
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
        public static TrainingData PlayForTrainingParallel(int n_game, Player player)
        {
            return PlayForTrainingParallel(n_game, player, false);
        }

        public static TrainingData PlayForTrainingParallel(int n_game, Player player, bool bi_result)
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
                int r = b.GetStoneCountGap();
                if (!bi_result)
                    return r;

                return Math.Max(-1, Math.Min(1, r));
            }

            int count = 0;

            var data = Enumerable.Range(0, 16).AsParallel().Select(i =>
            {
                var results = new TrainingData();
                var rand = new Random();

                while (count < n_game)
                {
                    Board board = Tester.CreateRandomGame(8, rand);
                    List<Board> boards = new List<Board>();

                    while (Step(ref board, boards, player, 1) | Step(ref board, boards, player, -1))
                    {
                    }
                    results.Add(boards, GetResult(board));

                    Interlocked.Increment(ref count);
                }

                return results;
            }).ToList().SelectMany(d => d);

            return new TrainingData(data.ToList());
        }

        public static TrainingData PlayForTraining(int n_game, Player player, Random rand, bool bi_result)
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
                int r = b.GetStoneCountGap();
                if (!bi_result)
                    return r;

                return Math.Max(-1, Math.Min(1, r));
            }

            var results = new TrainingData();

            for (int i = 0; i < n_game; i++)
            {
                Board board = Tester.CreateRandomGame(8, rand);
                List<Board> boards = new List<Board>();

                while (Step(ref board, boards, player, 1) | Step(ref board, boards, player, -1))
                {
                }
                results.Add(boards, GetResult(board));
            }

            return results;
        }
    }

    public class Trainer
    {
        public Weight Weights { get; }
        public float LearningRate { get; }

        public List<float> Log { get; } = new List<float>();

        public Trainer(Weight weights, float lr)
        {
            Weights = weights;
            LearningRate = lr;
        }

        public float Update(Board board, float result)
        {
            float e = result - Weights.EvalTraining(board);

            foreach (var b in new RotatedAndMirroredBoards(board))
            {
                Weights.UpdataEvaluation(b, e * LearningRate, 6);
            }

            Log.Add(e * e);

            return e;
        }

        public static List<float> Train(Weight weight, int depth, int n_games)
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
            }

            return trainer.Log;
        }
    }
}
