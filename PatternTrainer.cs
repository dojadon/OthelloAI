using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

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
                    Board board = Tester.CreateRnadomGame(rand, 8);
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
                Board board = Tester.CreateRnadomGame(rand, 8);
                List<Board> boards = new List<Board>();

                while (Step(ref board, boards, player, 1) | Step(ref board, boards, player, -1))
                {
                }
                results.Add(boards, GetResult(board));
            }

            return results;
        }
    }

    public class PatternTrainer
    {
        public PatternWeights Weights { get; }
        public float LearningRate { get; }

        public List<float> Log { get; } = new List<float>();

        public PatternTrainer(PatternWeights weights, float lr)
        {
            Weights = weights;
            LearningRate = lr;
        }

        public float Update(Board board, float result)
        {
            var boards = new RotatedAndMirroredBoards(board);

            float e = result - Weights.EvalTraining(board);

            foreach (var b in boards)
            {
                Weights.UpdataEvaluation(b, e * LearningRate, 6);
            }

            Log.Add(e * e);

            return e;
        }

        public static void Train(PatternWeights weights, float lr, RecordReader reader)
        {
            var trainer = new PatternTrainer(weights, lr);
            reader.OnLoadMove += (b, r) => trainer.Update(b, r);
            reader.OnLoadGame += i =>
            {
                if (i % 50000 == 0)
                    Console.WriteLine(i);
            };
            reader.Read();
        }
    }
}
