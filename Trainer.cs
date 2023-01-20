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
        public static TrainingData PlayForTrainingParallel(int n_game, Func<int, Player> create_player)
        {
            return new TrainingData(PlayForTrainingParallelSeparated(n_game, create_player).SelectMany(x => x));
        }

        public static TrainingData[] PlayForTrainingParallelSeparated(int n_game, Func<int, Player> create_player)
        {
            return PlayForTrainingParallelSeparated(n_game, create_player, create_player);
        }

        public static TrainingData[] PlayForTrainingParallelSeparated(int n_game, Func<int, Player> create_player1, Func<int, Player> create_player2)
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

            return n_game.Loop().AsParallel().Select(i =>
            {
                var p1 = create_player1(i);
                var p2 = create_player2(i);

                var rand = new Random();
                Board board = Board.Init;
                List<Board> boards = new List<Board>();

                while (Step(ref board, boards, p1, 1) | Step(ref board, boards, p2, -1))
                {
                }

                return new TrainingData() { { boards, GetResult(board) } };
            }).ToArray();
        }

        public static TrainingData PlayForTraining(int n_game, Player player1, Player player2)
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
                Board board = Board.Init;
                var boards = new List<Board>();

                while (Step(ref board, boards, player1, 1) | Step(ref board, boards, player2, -1))
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

        public float Train(IEnumerable<TrainingDataElement> train_data)
        {
            return train_data.Select(d => Update(d.board, d.result)).Select(x => x * x).Average();
        }

        public float TrainAndTest(IEnumerable<TrainingDataElement> train_data, IEnumerable<TrainingDataElement> valid_data, float depth = 0)
        {
            // Weight.Reset();

            foreach (var d in train_data)
                Update(d.board, d.result);

            return TestError(valid_data, depth);
        }

        public float TestError(IEnumerable<TrainingDataElement> valid_data, float depth = 0)
        {
            if(depth == 0)
                return valid_data.Select(d => TestError(d.board, d.result)).Select(x => x * x).Average();

            var player = new PlayerAI(new EvaluatorWeightsBased(Weight))
            {
                Params = new[] { new SearchParameterFactory(stage: 0, type: SearchType.Normal, depth: depth), },
                PrintInfo = false,
                Random = new Random(),
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

                return trainer.TestError(valid_data, depth);
            }).Average();
        }
    }
}
