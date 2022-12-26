using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OthelloAI.GA
{
    public interface IPopulationEvaluator<T, U> where U : Score<T>
    {
        public List<U> Evaluate(List<Individual<T>> pop);
    }

    public class PopulationEvaluatorRandomTournament<T> : IPopulationEvaluator<T, Score<T>>
    {
        public PopulationTrainer Trainer { get; }
        public int Depth { get; }
        public int EndStage { get; }
        public int NumGames { get; }

        public Func<Individual<T>, Individual<T>, int, (float, float)> GetDepthFraction { get; set; } = (_, _, _) => (0, 0);

        public PopulationEvaluatorRandomTournament(PopulationTrainer trainer, int depth, int endStage, int n_games)
        {
            Trainer = trainer;
            Depth = depth;
            EndStage = endStage;
            NumGames = n_games;
        }

        public PlayerAI CreatePlayer(Individual<T> ind1, Individual<T> ind2)
        {
            return new PlayerAI(ind1.CreateEvaluator())
            {
                Params = new[] { new SearchParameterFactory(stage: 0, type: SearchType.Normal, depth: i => GetDepthFraction(ind1, ind2, i).Item1),
                                              new SearchParameterFactory(stage: EndStage, type: SearchType.Normal, depth: 64)},
                PrintInfo = false,
            };
        }

        public int GetHashCode(Individual<T> ind)
        {
            static int GetHashCode(IEnumerable<TupleData<T>> tuples)
            {
                return tuples.Aggregate(0, (total, next) => HashCode.Combine(total, Board.BitCount(next.TupleBit)));
            }

            return ind.Tuples.Aggregate(0, (total, next) => HashCode.Combine(total, GetHashCode(next)));
        }

        public List<Individual<T>> SelectIndividualsToEvaluate(IEnumerable<(Individual<T> ind, float s)> pop, float rate)
        {
            IEnumerable<Individual<T>> SelectFromGroup(IEnumerable<(Individual<T> ind, float s)> group)
            {
                int n = (int)Math.Max(1, group.Count() * rate);
                return group.OrderBy(t => t.s).Select(t => t.ind).Take(n);
            }

            var groups = pop.GroupBy(t => GetHashCode(t.ind));

            return groups.Select(SelectFromGroup).SelectMany(x => x).ToList();
        }

        public List<Individual<T>> TrainAndSelectPop(List<Individual<T>> pop)
        {
            var set = pop.Distinct().ToArray();
            var scores = Trainer.Train(set.Select(ind => ind.Weight).ToList());

            return SelectIndividualsToEvaluate(set.Zip(scores, (ind, s) => (ind, s)), 0.5F);
        }

        public List<Score<T>> Evaluate(List<Individual<T>> pop)
        {
            var pop_to_evaluate = TrainAndSelectPop(pop);
            var scores = new Dictionary<Individual<T>, List<float>>(pop_to_evaluate.Select(ind => new KeyValuePair<Individual<T>, List<float>>(ind, new List<float>())));

            Parallel.For(0, NumGames, i =>
            {
                var rand = new Random();
                var pair = rand.SamplePair(pop_to_evaluate.Count);

                var ind1 = pop_to_evaluate[pair.Item1];
                var ind2 = pop_to_evaluate[pair.Item2];

                PlayerAI p1 = CreatePlayer(ind1, ind2);
                PlayerAI p2 = CreatePlayer(ind2, ind1);

                Board b = Tester.PlayGame(p1, p2, Tester.CreateRandomGame(2, rand));
                int result = b.GetStoneCountGap();

                if (result > 0)
                {
                    scores[ind1].Add(1);
                    scores[ind2].Add(0);
                }
                else if (result < 0)
                {
                    scores[ind1].Add(0);
                    scores[ind2].Add(1);
                }
            });

            return pop.Select(ind => new Score<T>(ind, -scores[ind].Average())).ToList();
        }
    }

    public class PopulationEvaluatorTournament<T> : IPopulationEvaluator<T, Score<T>>
    {
        public PopulationTrainer Trainer { get; }
        public int Depth { get; }
        public int EndStage { get; }

        public PopulationEvaluatorTournament(PopulationTrainer trainer, int depth, int endStage)
        {
            Trainer = trainer;
            Depth = depth;
            EndStage = endStage;
        }

        public PlayerAI CreatePlayer(Individual<T> ind)
        {
            return new PlayerAI(ind.CreateEvaluator())
            {
                Params = new[] { new SearchParameterFactory(stage: 0, type: SearchType.Normal, depth: Depth),
                                              new SearchParameterFactory(stage: EndStage, type: SearchType.Normal, depth: 64)},
                PrintInfo = false,
            };
        }

        public static (int, int)[] Combinations(int n)
        {
            return Enumerable.Range(0, n - 1).SelectMany(i => Enumerable.Range(i + 1, n - i - 1).Select(j => (i, j))).ToArray();
        }

        public List<Score<T>> Evaluate(List<Individual<T>> pop)
        {
            Trainer.Train(pop.Select(ind => ind.Weight).ToList());

            foreach (var ind in pop)
            {
                ind.Log.Clear();
            }

            Parallel.ForEach(Combinations(pop.Count), pair =>
            {
                var ind1 = pop[pair.Item1];
                var ind2 = pop[pair.Item2];

                PlayerAI p1 = CreatePlayer(ind1);
                PlayerAI p2 = CreatePlayer(ind2);

                for (int k = 0; k < 2; k++)
                {
                    Board b;
                    int result;

                    if (k == 0)
                    {
                        b = Tester.PlayGame(p1, p2, Board.Init);
                        result = b.GetStoneCountGap();
                    }
                    else
                    {
                        b = Tester.PlayGame(p2, p1, Board.Init);
                        result = -b.GetStoneCountGap();
                    }

                    if (result > 0)
                    {
                        ind1.Log.Add(1);
                        ind2.Log.Add(0);
                    }
                    else if (result < 0)
                    {
                        ind1.Log.Add(0);
                        ind2.Log.Add(1);
                    }
                }
            });

            return pop.Select(ind => new Score<T>(ind, ind.Log.Average())).ToList();
        }
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
            var scores = Trainer.Train(pop.Select(ind => ind.Weight).ToList());
            return pop.Zip(scores, (ind, s) => new Score<T>(ind, s)).ToList();
        }
    }

    public class PopulationEvaluatorDistributed<T, U> : IPopulationEvaluator<T, U> where U : Score<T>
    {
        public IPopulationEvaluator<T, U>[] Evaluators { get; init; }

        public List<U> Evaluate(List<Individual<T>> pop)
        {
            int size = pop.Count / Evaluators.Length;
            return Evaluators.SelectMany((e, i) => e.Evaluate(pop.Skip(size * i).Take(size).ToList())).ToList();
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
                var trainer = new Trainer(ind.Weight, 0.001F);

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
            float Eval(Individual<T> ind) => Trainer.KFoldTest(ind.Weight, ind.GetDepth(), Data);

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

            var trainer = new Trainer(ind.Weight, 0.001F);
            trainer.Train(train_data);

            float score = trainer.TestError(depth, valid_data);

            return new Score<T>(ind, score);
        }

        public List<Score<T>> Evaluate(List<Individual<T>> pop)
        {
            return pop.AsParallel().WithDegreeOfParallelism(Program.NumThreads)
                .Select((ind, i) =>
                {
                    int index = i / 100;

                    if (index == 0)
                        return new Score<T>(ind, Trainer.KFoldTest(ind.Weight, ind.GetDepth(), Data));
                    else
                        return TrainAndTest(ind, ind.GetDepth(), index - 1);
                }).ToList();
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

    public abstract class PopulationTrainer
    {
        public int Depth { get; }
        public int EndStage { get; }
        public int NumGames { get; }

        public int NumThreads { get; }

        public PopulationTrainer(int depth, int endStage, int numGames, int numThreads)
        {
            Depth = depth;
            EndStage = endStage;
            NumGames = numGames;
            NumThreads = numThreads;
        }

        public PlayerAI CreatePlayer(Evaluator e)
        {
            return new PlayerAI(e)
            {
                Params = new[] { new SearchParameterFactory(stage: 0, type: SearchType.Normal, depth: Depth),
                                              new SearchParameterFactory(stage: EndStage, type: SearchType.Normal, depth: 64)},
                PrintInfo = false,
            };
        }

        public PlayerAI CreatePlayer(Weight p)
        {
            return CreatePlayer(new EvaluatorWeightsBased(p));
        }

        public abstract List<float> Train(List<Weight> pop);
    }

    public class PopulationTrainerCoLearning : PopulationTrainer
    {
        public FixedQueue<TrainingData> TrainingData { get; } = new FixedQueue<TrainingData>(10000);

        public PopulationTrainerCoLearning(int depth, int endStage, int numGames, int sizeExperiments, int numThreads) : base(depth, endStage, numGames, numThreads)
        {
            TrainingData = new FixedQueue<TrainingData>(sizeExperiments);
        }

        public override List<float> Train(List<Weight> pop)
        {
            var evaluator = new EvaluatorRandomChoice(pop.Select(p => new EvaluatorWeightsBased(p)).ToArray());
            Player player = CreatePlayer(evaluator);

            foreach (var w in pop)
                w.Reset();

            var trainers = pop.Select(p => new Trainer(p, 0.001F)).ToArray();

            if (TrainingData.Count > 0)
            {
                Parallel.ForEach(trainers, trainer =>
                {
                    foreach (var t in TrainingData.SelectMany(x => x))
                        trainer.Update(t.board, t.result);
                });
            }

            for (int i = 0; i < NumGames / 16; i++)
            {
                var data = TrainerUtil.PlayForTrainingParallelSeparated(16, player, NumThreads);

                //foreach (var t in data)
                //    TrainingData.Enqueue(t);

                Parallel.ForEach(trainers, trainer =>
                {
                    foreach (var t in data.SelectMany(x => x))
                        trainer.Update(t.board, t.result);
                });

                // Console.WriteLine($"{i} / {NumGames / 16}");
            }

            return trainers.Select(trainer => trainer.Log.TakeLast(trainer.Log.Count / 4).Average()).ToList();
        }
    }
}
