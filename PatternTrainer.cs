using OthelloAI.Patterns;
using System;
using System.Linq;

namespace OthelloAI
{
    class PatternTrainer
    {
        public Pattern[] Patterns { get; }
        public float LearningRate { get; }

        public PatternTrainer(Pattern[] patterns, float lr)
        {
            Patterns = patterns;
            LearningRate = lr;
        }

        public void Update(Board board, int result)
        {
            var boards = new Boards(board);

            float e = result - Patterns.Sum(p => p.EvalTrainingByPEXTHashing(boards));
            Array.ForEach(Patterns, p => p.UpdataEvaluation(boards, e * LearningRate));
        }

        public void Update2(Board board, int result)
        {
            var boards = new Boards(board);
            Array.ForEach(Patterns, p => p.UpdateWinCount(boards, result > 0, 1));
        }

        public static void Train(Pattern[] patterns, float lr, RecordReader reader)
        {
            var trainer = new PatternTrainer(patterns, lr);
            reader.OnLoadMove += trainer.Update2;
            reader.OnLoadGame += i =>
            {
                if (i % 50000 == 0)
                    Console.WriteLine(i);
            };
            reader.Read();
        }
    }
}
