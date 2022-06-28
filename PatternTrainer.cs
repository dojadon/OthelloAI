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

        public void Add(IEnumerable<Board> boards, int result)
        {
            AddRange(boards.Select(b => new TrainingDataElement(b, result)));
        }
    }

    public struct TrainingDataElement
    {
        public readonly Board board;
        public readonly int result;

        public TrainingDataElement(Board board, int result)
        {
            this.board = board;
            this.result = result;
        }
    }

    public class TrainerUtil
    {
        public static TrainingData PlayForTrainingParallel(int n_game, Player player)
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

            int count = 0;

            var data = Enumerable.Range(0, 16).AsParallel().SelectMany(i =>
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
                    results.Add(boards, board.GetStoneCountGap());

                    Interlocked.Increment(ref count);
                }

                return results;
            });

            return new TrainingData(data.ToList());
        }
    }


    class RegionTrainer
    {
        public RegionForTraining Region { get; }

        public RegionTrainer(RegionForTraining region)
        {
            Region = region;
        }

        public void Update(Board board, int result)
        {
            if (board.n_stone < 20 || board.n_stone > 44)
                return;

            Region.UpdateWinCountWithRotatingAndFliping(board, result > 0 ? 1 : 0, 1);
        }

        public static void Train(RegionForTraining region, RecordReader reader)
        {
            var trainer = new RegionTrainer(region);
            reader.OnLoadMove += trainer.Update;
            reader.OnLoadGame += i =>
            {
                if (i % 10000 == 0)
                    Console.WriteLine(i);
            };
            reader.Read();
        }
    }

    public class PatternTrainer
    {
        public Pattern[] Patterns { get; }
        public float LearningRate { get; }

        public PatternTrainer(Pattern[] patterns, float lr)
        {
            Patterns = patterns;
            LearningRate = lr;
        }

        public float Update(Board board, int result)
        {
            var boards = new RotatedAndMirroredBoards(board);

            float e = result - Patterns.Sum(p => p.EvalTrainingByPEXTHashing(boards));

            foreach (var p in Patterns)
                foreach (var b in boards)
                {
                    p.UpdataEvaluation(b, e * LearningRate);
                }

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
    }
}
