using System;
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

    public class PopulationEvaluatorTournament<T> : IPopulationEvaluator<T, Score<T>>
    {
        public PopulationTrainer Trainer { get; }
        public int NumTrainGames { get; }
        public int NumTestGames { get; }

        public int Depth { get; init; }
        public int Endgame { get; init; }

        public PopulationEvaluatorTournament(PopulationTrainer trainer, int numTrainGames, int numTestGames, int depth, int endgame)
        {
            Trainer = trainer;
            NumTrainGames = numTrainGames;
            NumTestGames = numTestGames;
            Depth = depth;
            Endgame = endgame;
        }

        public List<Score<T>> Evaluate(List<Individual<T>> pop)
        {
            var weights = pop.Select(ind => ind.CreateWeight()).ToArray();
            Trainer.Train(weights, NumTrainGames);

            var scores = weights.AsParallel().Select(w => TestWeight(w, weights)).ToArray();

            return pop.Zip(scores, (ind, s) => new Score<T>(ind, -s)).ToList();
        }

        public float TestWeight(Weight weight, Weight[] weights)
        {
            int n_games = NumTestGames / 2 / weights.Length;
            var rand = new Random();

            return n_games.Loop(_ =>
            {
                var w2 = rand.Choice(weights);
                return (PlayGame(weight, w2) + -PlayGame(w2, weight)) * 0.5F;
            }).Average();
        }

        PlayerAI CreatePlayer(Weight w, int d, int endgame)
        {
            return new PlayerAI(new EvaluatorRandomize(new EvaluatorWeightsBased(w), 5F))
            {
                Params = new[] {
                        new SearchParameterFactory(stage: 0, type: SearchType.IterativeDeepening, depth: d),
                        new SearchParameterFactory(stage: endgame, type: SearchType.Normal, depth: 64),
                    },
                PrintInfo = false,
            };
        }

        public float PlayGame(Weight w1, Weight w2)
        {
            var b = TrainerUtil.PlayForTraining(1, CreatePlayer(w1, Depth, Endgame), CreatePlayer(w2, Depth, Endgame));
            return Math.Clamp(b[^1].result, -1, 1);
        }
    }

    public class PopulationEvaluatorTrainingScorebySelfMatch<T> : IPopulationEvaluator<T, Score<T>>
    {
        PopulationTrainer Trainer { get; }
        int NumTrainGames { get; }
        int NumTestGames { get; }

        public FineTuner Tuner { get; set;  }

        public PopulationEvaluatorTrainingScorebySelfMatch(PopulationTrainer trainer, int numTrainGames, int numTestGames)
        {
            Trainer = trainer;
            NumTrainGames = numTrainGames;
            NumTestGames = numTestGames;
        }

        public List<Score<T>> Evaluate(List<Individual<T>> pop)
        {
            Weight[] weights  = pop.AsParallel().Select(ind => ind.CreateWeightWithFineTuning(Tuner)).ToArray();

            var scores = Trainer.TrainAndTest(pop.Select(ind => ind.CreateWeight()).ToArray(), NumTrainGames, NumTestGames);
            return pop.Zip(scores, (ind, s) => new Score<T>(ind, s)).ToList();
        }
    }

    public class PopulationEvaluatorDistributed<T, U> : IPopulationEvaluator<T, U> where U : Score<T>
    {
        public IPopulationEvaluator<T, U>[] Evaluators { get; init; }

        public List<U> Evaluate(List<Individual<T>> pop)
        {
            if (Evaluators.Length == 1)
                return Evaluators[0].Evaluate(pop);

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

    public class PopulationEvaluatorTrainingScoreShuffledKFold<T> : IPopulationEvaluator<T, Score<T>>
    {
        TrainingData[] Data { get; }
        public int NumSamplesForTraining { get; }
        public int NumSamplesForValidation { get; }

        public FineTuner Tuner { get; set; }

        public PopulationEvaluatorTrainingScoreShuffledKFold(TrainingData[] data, float r_train, float r_valid) : this(data, (int)(data.Length * r_train), (int)(data.Length * r_valid))
        {
        }

        public PopulationEvaluatorTrainingScoreShuffledKFold(TrainingData[] data, int n_train, int n_valid)
        {
            Data = data;
            NumSamplesForTraining = n_train;
            NumSamplesForValidation = n_valid;
        }

        public Score<T> TrainAndTest(Individual<T> ind)
        {
            var rand = new Random();
            var indices_train = new HashSet<int>(rand.Sample(Data.Length.Loop(), NumSamplesForTraining));
            var indices_valid = new HashSet<int>(rand.Sample(Data.Length.Loop(), NumSamplesForValidation));

            var train_data = Enumerable.Range(0, Data.Length).Where(indices_train.Contains).SelectMany(i => Data[i]);
            var valid_data = Enumerable.Range(0, Data.Length).Where(indices_valid.Contains).SelectMany(i => Data[i]);

            var trainer = new Trainer(ind.CreateWeightWithFineTuning(Tuner), 0.001F);
            trainer.Train(train_data);

            float score = trainer.TestError(valid_data);

            return new Score<T>(ind, score);
        }

        public List<Score<T>> Evaluate(List<Individual<T>> pop)
        {
            return pop
                .AsParallel()
                // .WithDegreeOfParallelism(Program.NumThreads)
                .Select(TrainAndTest).ToList();
        }
    }

    public class PopulationEvaluatorTrainingScoreShuffledKFold2<T> : IPopulationEvaluator<T, Score<T>>
    {
        TrainingDataElement[] Data { get; }
        public int NumSamplesForTraining { get; }
        public int NumSamplesForValidation { get; }

        public PopulationEvaluatorTrainingScoreShuffledKFold2(TrainingDataElement[] data, float r_train, float r_valid) : this(data, (int)(data.Length * r_train), (int)(data.Length * r_valid))
        {
        }

        public PopulationEvaluatorTrainingScoreShuffledKFold2(TrainingDataElement[] data, int n_train, int n_valid)
        {
            Data = data;
            NumSamplesForTraining = n_train;
            NumSamplesForValidation = n_valid;
        }

        public Score<T> TrainAndTest(Individual<T> ind, IEnumerable<TrainingDataElement> train_data, IEnumerable<TrainingDataElement> valid_data)
        {
            var trainer = new Trainer(ind.CreateWeight(), 0.001F);
            trainer.Train(train_data);

            float score = trainer.TestError(valid_data);

            return new Score<T>(ind, score);
        }

        public List<Score<T>> Evaluate(List<Individual<T>> pop)
        {
            var rand = new Random();
            var indices_train = new HashSet<int>(rand.Sample(Data.Length.Loop(), NumSamplesForTraining));
            var indices_valid = new HashSet<int>(rand.Sample(Data.Length.Loop(), NumSamplesForValidation));

            var train_data = Enumerable.Range(0, Data.Length).Where(indices_train.Contains).Select(i => Data[i]);
            var valid_data = Enumerable.Range(0, Data.Length).Where(indices_valid.Contains).Select(i => Data[i]);

            return pop
                .AsParallel()
                // .WithDegreeOfParallelism(Program.NumThreads)
                .Select(ind => TrainAndTest(ind, train_data, valid_data)).ToList();
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

        public int StartNumDiscs { get; } = 30;
        public int EndNumDiscs { get; } = 54;

        public PopulationTrainer(int depth, int endStage)
        {
            Depth = depth;
            EndStage = endStage;
        }

        bool Within(TrainingDataElement d) => StartNumDiscs <= d.board.n_stone && d.board.n_stone <= EndNumDiscs;

        Player CreateRandomPlayer(Weight[] weights)
        {
            var rand = new Random();
            var w = rand.Choice(weights);

            var evaluator = new EvaluatorRandomize(new EvaluatorWeightsBased(w), v: 8);
            return new PlayerAI(evaluator)
            {
                PrintInfo = false,
                Params = new[] {
                        new SearchParameterFactory(stage: 0, SearchType.Normal, Depth),
                        new SearchParameterFactory(stage: EndStage, SearchType.Normal, 64),
                    },
            };
        }

        public void Train(Weight[] weights, int n_train)
        {
            var trainers = weights.Select(w => new Trainer(w, 0.0005F)).ToArray();

            for (int i = 0; i < n_train / 32; i++)
            {
                var data = TrainerUtil.PlayForTrainingParallel(32, _ => CreateRandomPlayer(weights)).Where(Within).ToArray();
                Parallel.ForEach(trainers, t => t.Train(data));

                //Console.WriteLine($"{i} / {n_train / 16}");
                //Console.WriteLine($"{i} / {NumTrainingGames / 16}: " + string.Join(", ", trainers.Select(t => t.Log.TakeLast(t.Log.Count / 4).Average())));
            }
        }

        public List<float> TrainAndTest(Weight[] weights, int n_train, int n_test)
        {
            Train(weights, n_train);

            var trainers = weights.Select(w => new Trainer(w, 0.0005F)).ToArray();
            var test_data = TrainerUtil.PlayForTrainingParallel(n_test, _ => CreateRandomPlayer(weights)).Where(Within).ToArray();

            return trainers.AsParallel().Select(t => t.TestError(test_data)).ToList();
        }
    }
}
