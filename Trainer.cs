using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
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

        public float TestError(TrainingDataElement d)
        {
            return TestError(d.board, d.result);
        }

        public float TestError(Board board, float result)
        {
            var boards = new RotatedAndMirroredBoards(board);
            float e = result - Weight.EvalTraining(boards);
            return e;
        }

        public float UpdateWithBatch(TrainingData data)
        {
            float e = data.Select(TestError).Average();

            foreach (var b in data.SelectMany(d => new RotatedAndMirroredBoards(d.board)))
            {
                Weight.UpdataEvaluation(b, e * LearningRate, Weight.WEIGHT_RANGE);
            }

            Log.Add(e * e);

            return e;
        }

        public float Update(TrainingDataElement d)
        {
            return Update(d.board, d.result);
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

        public void Train(IEnumerable<TrainingDataElement> train_data)
        {
            foreach (var d in train_data)
                Update(d.board, d.result);
        }

        public float TrainAndTest(IEnumerable<TrainingDataElement> train_data, IEnumerable<TrainingDataElement> valid_data, float depth=0)
        {
            Weight.Reset();

            foreach (var d in train_data)
                Update(d.board, d.result);

            if(depth > 0)
                return TestError(depth, valid_data);
            else
                return valid_data.Select(d => TestError(d.board, d.result)).Select(x => x * x).Average();
        }

        public float TestError(float depth, IEnumerable<TrainingDataElement> valid_data)
        {
            var player = new PlayerAI(new EvaluatorWeightsBased(Weight))
            {
                Params = new[] { new SearchParameterFactory(stage: 0, type: SearchType.Normal, depth: depth), },
                PrintInfo = false,
            };

            float EvalError(TrainingDataElement t)
            {
                float eval = player.Evaluate(t.board);
                return (eval - t.result) * (eval - t.result);
            }

            return valid_data.Select(EvalError).Average();
        }

        public static float KFoldTest(Weight weight, float depth, TrainingData[] data)
        {
            return Enumerable.Range(0, data.Length).AsParallel().Select(i =>
            {
                var trainer = new Trainer(weight.Copy(), 0.001F);

                var train_data = data.Length.Loop().Where(j => i != j).SelectMany(j => data[j]);
                var valid_data = data[i];

                trainer.Train(train_data);

                return trainer.TestError(depth, valid_data);
            }).Average();
        }
    }
}
