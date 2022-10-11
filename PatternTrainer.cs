using OthelloAI.Patterns;
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

            for(int i = 0; i < n_game; i++)
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
        public Pattern[] Patterns { get; }
        public float LearningRate { get; }

        public List<float> Log { get; } = new List<float>();

        public PatternTrainer(Pattern[] patterns, float lr)
        {
            Patterns = patterns;
            LearningRate = lr;
        }

        public float Test(Board board, float result)
        {
            var boards = new RotatedAndMirroredBoards(board);
            return result - Patterns.Sum(p => p.EvalTrainingByPEXTHashing(boards));
        }

        public static float HyperbolicTangent(float f)
        {
            double e1 = Math.Pow(Math.E, f);
            double e2 = 1 / e1;

            return (float)((e1 - e2) / (e1 + e2));
        }

        public float UpdateTDL(Board current, Board next)
        {
            var boards1 = new RotatedAndMirroredBoards(current);
            var boards2 = new RotatedAndMirroredBoards(next);

            float e1 = HyperbolicTangent(Patterns.Sum(p => p.EvalTrainingByPEXTHashing(boards1)));
            float e2 = HyperbolicTangent(Patterns.Sum(p => p.EvalTrainingByPEXTHashing(boards2)));

            float e = (e2 - e1) * (1 - e1 * e1);

            foreach (var p in Patterns)
                foreach (var b in boards1)
                    p.UpdataEvaluation(b, e * LearningRate, 0.5F);

            return e;
        }

        public float UpdateTDL(Board current, float result)
        {
            var boards1 = new RotatedAndMirroredBoards(current);

            float e1 = HyperbolicTangent(Patterns.Sum(p => p.EvalTrainingByPEXTHashing(boards1)));
            float e2 = result;

            float e = (e2 - e1) * (1 - e1 * e1);

            foreach (var p in Patterns)
                foreach (var b in boards1)
                    p.UpdataEvaluation(b, e * LearningRate, 0.5F);

            return e;
        }

        public float UpdateTDL(TrainingDataElement d)
        {
            return UpdateTDL(d.board, d.result);
        }

        public float Update(Board board, float result)
        {
            var boards = new RotatedAndMirroredBoards(board);

            float e = result - Patterns.Sum(p => p.EvalTrainingByPEXTHashing(boards));

            foreach (var p in Patterns)
                foreach (var b in boards)
                {
                    p.UpdataEvaluation(b, e * LearningRate, 6);
                }

            Log.Add(e * e);

            return e;
        }

        public static void Train(Pattern[] patterns, float lr, RecordReader reader)
        {
            var trainer = new PatternTrainer(patterns, lr);
            reader.OnLoadMove += (b, r) => trainer.Update(b, r);
            reader.OnLoadGame += i =>
            {
                if (i % 50000 == 0)
                    Console.WriteLine(i);
            };
            reader.Read();
        }

        public static void Train(Pattern[] patterns, float lr, PlayerAI player)
        {
            var trainer = new PatternTrainer(patterns, lr);
            var log = new List<float>();

            for (int i = 0; i < 10000; i++)
            {
                var data = TrainerUtil.PlayForTrainingParallel(16, player, true);
                var errors = data.Select(trainer.UpdateTDL);

                log.Add(errors.Select(e => e * e).Average());

                Console.WriteLine(log.TakeLast(100).Average() * 100);

                if (i % 500 == 0)
                {
                    foreach (var p in patterns)
                        p.Save();
                }
            }
        }

        public static void TrainTDL(Pattern[] patterns, float lr, PlayerAI player)
        {
            var rand = new Random();

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

            var trainer = new PatternTrainer(patterns, lr);
            var log = new List<float>();

            for (int i = 0; i < 10000; i++)
            {
                Board board = Tester.CreateRnadomGame(rand, 8);
                Board prev = board;
                int s = 1;

                var errors = new List<float>();

                while (true)
                {
                    if (!Step(ref board, player, s) && !Step(ref board, player, s = -s))
                        break;

                    if (prev.n_stone < 56)
                    {
                        errors.Add(trainer.UpdateTDL(prev, board));
                        prev = board;
                    }
                    s = -s;
                }

                int result = board.GetStoneCountGap();
                result = result == 0 ? 0 : result > 0 ? 1 : -1;
                errors.Add(trainer.UpdateTDL(prev, result));

                float error = errors.Select(e => e * e).Average();
                log.Add(error);

                Console.WriteLine(log.TakeLast(100).Average() * 100);

                if (i % 500 == 0)
                {
                    foreach (var p in patterns)
                        p.Save();
                }
            }
        }
    }
}
