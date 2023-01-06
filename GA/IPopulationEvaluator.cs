﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace OthelloAI.GA
{
    public interface IPopulationEvaluator<T, U> where U : Score<T>
    {
        public List<U> Evaluate(List<Individual<T>> pop);
    }

    public class PopulationEvaluatorTrainingScorebySelfMatch<T> : IPopulationEvaluator<T, Score<T>>
    {
        PopulationTrainer Trainer { get; }

        public PopulationEvaluatorTrainingScorebySelfMatch(PopulationTrainer trainer)
        {
            Trainer = trainer;
        }

        public List<Score<T>> Evaluate(List<Individual<T>> pop)
        {
            var scores = Trainer.Train(pop.Select(ind => ind.CreateWeight()).ToList());
            return pop.Zip(scores, (ind, s) => new Score<T>(ind, s)).ToList();
        }
    }

    public class PopulationEvaluatorDistributed<T, U> : IPopulationEvaluator<T, U> where U : Score<T>
    {
        public IPopulationEvaluator<T, U>[] Evaluators { get; init; }

        public List<U> Evaluate(List<Individual<T>> pop)
        {
            int size = pop.Count / Evaluators.Length;
            return Evaluators.AsParallel().SelectMany((e, i) => e.Evaluate(pop.Skip(size * i).Take(size).ToList())).ToList();
        }
    }

    public class PopulationEvaluatorTrainingScore<T> : IPopulationEvaluator<T, Score<T>>
    {
        TrainingData TrainingData { get; }
        TrainingData ValidationData { get; }

        public PopulationEvaluatorTrainingScore(IEnumerable<TrainingDataElement> train, IEnumerable<TrainingDataElement> valid)
        {
            TrainingData = new TrainingData(train);
            ValidationData = new TrainingData(valid);
        }

        public List<Score<T>> Evaluate(List<Individual<T>> pop)
        {
            return pop.AsParallel().WithDegreeOfParallelism(Program.NumThreads).Select(ind =>
            {
                var trainer = new Trainer(ind.CreateWeight(), 0.001F);

                float score = trainer.TrainAndTest(TrainingData, ValidationData, ind.GetDepth());

                return new Score<T>(ind, score);
            }).ToList();
        }
    }

    public class PopulationEvaluatorTrainingScoreKFold<T> : IPopulationEvaluator<T, Score<T>>
    {
        TrainingData[] Data { get; }

        public PopulationEvaluatorTrainingScoreKFold(TrainingData[] data)
        {
            Data = data;
        }

        public List<Score<T>> Evaluate(List<Individual<T>> pop)
        {
            float Eval(Individual<T> ind) => Trainer.KFoldTest(ind.CreateWeight(), ind.GetDepth(), Data);

            return pop.AsParallel().WithDegreeOfParallelism(Program.NumThreads).Select(ind => new Score<T>(ind, Eval(ind))).ToList();
        }
    }

    public class PopulationEvaluatorTrainingScoreKFoldWithVariableDepth<T> : IPopulationEvaluator<T, Score<T>>
    {
        TrainingData[] Data { get; }

        public PopulationEvaluatorTrainingScoreKFoldWithVariableDepth(TrainingData[] data)
        {
            Data = data;
        }

        public Score<T> TrainAndTest(Individual<T> ind, float depth, int index)
        {
            var train_data = Enumerable.Range(0, Data.Length).Where(i => i != index).SelectMany(i => Data[i]);
            var valid_data = Data[index];

            var trainer = new Trainer(ind.CreateWeight(), 0.001F);
            trainer.Train(train_data);

            float score = 2.Loop(_ => trainer.TestError(valid_data, depth)).Average();

            return new Score<T>(ind, score);
        }

        public List<Score<T>> Evaluate(List<Individual<T>> pop)
        {
            return pop.AsParallel().WithDegreeOfParallelism(Program.NumThreads)
                .Select((ind, i) =>
                {
                    int index = i / 100;

                    //if (index == 0)
                    //    return new Score<T>(ind, Trainer.KFoldTest(ind.Weight, ind.GetDepth(), Data));
                    //else
                    //    return TrainAndTest(ind, ind.GetDepth(), index - 1);

                    return TrainAndTest(ind, ind.GetDepth(), index);
                }).ToList();
        }
    }

    public class PopulationEvaluatorTrainingScoreShuffleKFold<T> : IPopulationEvaluator<T, Score<T>>
    {
        TrainingData Data { get; }
        public int NumSamples { get; }

        public PopulationEvaluatorTrainingScoreShuffleKFold(TrainingData data, float r)
        {
            Data = data;
            NumSamples = (int)(data.Count * r);
        }

        public Score<T> TrainAndTest(Individual<T> ind, float depth)
        {
            var rand = new Random();
            var indices = new HashSet<int>(rand.Sample(Data.Count.Loop(), NumSamples));

            var train_data = Enumerable.Range(0, Data.Count).Where(i => !indices.Contains(i)).Select(i => Data[i]);
            var valid_data = Enumerable.Range(0, Data.Count).Where(indices.Contains).Select(i => Data[i]);

            var trainer = new Trainer(ind.CreateWeight(), 0.001F);
            trainer.Train(train_data);

            float score = trainer.TestError(valid_data, depth);

            return new Score<T>(ind, score);
        }

        public List<Score<T>> Evaluate(List<Individual<T>> pop)
        {
            return pop
                .AsParallel().WithDegreeOfParallelism(Program.NumThreads)
                .Select(ind => TrainAndTest(ind, ind.GetDepth())).ToList();
        }
    }

    public class PopulationEvaluatorNSGA2<T> : IPopulationEvaluator<T, ScoreNSGA2<T>>
    {
        public IPopulationEvaluator<T, Score<T>> Evaluator1 { get; set; }
        public IPopulationEvaluator<T, Score<T>> Evaluator2 { get; set; }

        public List<(Score2D<T> s, float c)> CalcCongection(List<Score2D<T>> scores, float range1, float range2)
        {
            if (scores.Count == 1)
                return new List<(Score2D<T> s, float c)>() { (scores[0], scores[0].score * scores[0].score) };

            float Distance(Score2D<T> i1, Score2D<T> i2)
            {
                float s1 = (i1.score - i2.score) / range1;
                float s2 = (i1.score2 - i2.score2) / range2;
                return (float)Math.Sqrt(s1 * s1 + s2 * s2);
            }

            var ordered = scores.OrderBy(s => s.score).ToList();

            var dist = ordered.Take(scores.Count - 1).Zip(ordered.Skip(1), Distance).ToArray();

            var c0 = new float[] { dist.Max() };
            var c1 = dist.Concat(c0);
            var c2 = c0.Concat(dist);

            return ordered.ZipThree(c1, c2, (s, f1, f2) => (s, f1 + f2)).ToList();
        }

        public List<ScoreNSGA2<T>> NonDominatedSort(List<Score2D<T>> scores)
        {
            bool Dominated(Score2D<T> s1, Score2D<T> s2)
            {
                return s1.score > s2.score && s1.score2 > s2.score2;
            }

            bool IsNonDominated(Score2D<T> s)
            {
                return scores.Count(s2 => Dominated(s, s2)) == 0;
            }

            var scoreNSGA2 = new List<ScoreNSGA2<T>>();
            int rank = 0;

            float range1 = scores.Max(s => s.score) - scores.Min(s => s.score);
            float range2 = scores.Max(s => s.score2) - scores.Min(s => s.score2);

            while (scores.Count > 0)
            {
                var rank0 = scores.Where(IsNonDominated).ToList();
                scores.RemoveAll(i => rank0.Contains(i));

                var cong = CalcCongection(rank0, range1, range2);
                scoreNSGA2.AddRange(cong.Select(t => new ScoreNSGA2<T>(t.s.ind, t.s.score, t.s.score2, rank, -t.c)));

                rank++;
            }

            return scoreNSGA2;
        }

        public List<ScoreNSGA2<T>> Evaluate(List<Individual<T>> pop)
        {
            var score1 = Evaluator1.Evaluate(pop);
            var score2 = Evaluator2.Evaluate(pop);

            var score = score1.Zip(score2).Select(t => new Score2D<T>(t.First.ind, t.First.score, t.Second.score)).ToList();

            return NonDominatedSort(score);
        }
    }

    public class PopulationTrainer
    {
        public int Depth { get; }
        public int EndStage { get; }
        public int NumTrainingGames { get; }
        public int NumTestGames { get; }

        public int StartNumDiscs { get; } = 30;
        public int EndNumDiscs { get; } = 54;

        public PopulationTrainer(int depth, int endStage, int numTrainingGames, int numTestGames)
        {
            Depth = depth;
            EndStage = endStage;
            NumTrainingGames = numTrainingGames;
            NumTestGames = numTestGames;
        }

        public List<float> Train(IEnumerable<Weight> pop)
        {
            var evaluator = new EvaluatorRandomize(new EvaluatorRandomChoice(pop.Select(p => new EvaluatorWeightsBased(p)).ToArray()), v: 5F);
            Player player = new PlayerAI(evaluator)
            {
                PrintInfo = false,
                Params = new[] {
                    new SearchParameterFactory(stage: 0, SearchType.Normal, Depth),
                    new SearchParameterFactory(stage: EndStage, SearchType.Normal, 64),
                },
            };
            var trainers = pop.Select(w => new Trainer(w, 0.001F)).ToArray();

            bool Within(TrainingDataElement d) => StartNumDiscs <= d.board.n_stone && d.board.n_stone <= EndNumDiscs;

            for (int i = 0; i < NumTrainingGames / 16; i++)
            {
                var data = TrainerUtil.PlayForTrainingParallel(16, player).Where(Within).ToArray();
                Parallel.ForEach(trainers, t => t.Train(data));

                // Console.WriteLine($"{i} / {NumTrainingGames / 16}");
            }

            var test_data = TrainerUtil.PlayForTrainingParallel(NumTestGames, player).Where(Within).ToArray();

            return trainers.AsParallel().Select(t => t.TestError(test_data, 1)).ToList();
        }
    }
}
