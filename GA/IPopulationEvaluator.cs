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
                Params = new[] { new SearchParameters(stage: 0, type: SearchType.Normal, depth: i => GetDepthFraction(ind1, ind2, i).Item1),
                                              new SearchParameters(stage: EndStage, type: SearchType.Normal, depth: 64)},
                PrintInfo = false,
            };
        }

        public List<Score<T>> Evaluate(List<Individual<T>> pop)
        {
            Trainer.Train(pop.Select(ind => ind.Weights).ToList());

            foreach (var ind in pop)
            {
                ind.Log.Clear();
            }

            Parallel.For(0, NumGames, i =>
            {
                var rand = new Random();
                var pair = rand.SamplePair(pop.Count);

                var ind1 = pop[pair.Item1];
                var ind2 = pop[pair.Item2];

                PlayerAI p1 = CreatePlayer(ind1, ind2);
                PlayerAI p2 = CreatePlayer(ind2, ind1);

                Board b = Tester.PlayGame(p1, p2, Tester.CreateRandomGame(6, rand));
                int result = b.GetStoneCountGap();

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
            });

            return pop.Select(ind => new Score<T>(ind, -ind.Log.Average())).ToList();
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
                Params = new[] { new SearchParameters(stage: 0, type: SearchType.Normal, depth: Depth),
                                              new SearchParameters(stage: EndStage, type: SearchType.Normal, depth: 64)},
                PrintInfo = false,
            };
        }

        public static (int, int)[] Combinations(int n)
        {
            return Enumerable.Range(0, n - 1).SelectMany(i => Enumerable.Range(i + 1, n - i - 1).Select(j => (i, j))).ToArray();
        }

        public List<Score<T>> Evaluate(List<Individual<T>> pop)
        {
            Trainer.Train(pop.Select(ind => ind.Weights).ToList());

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

    public class PopulationEvaluatorTrainingScore<T> : IPopulationEvaluator<T, Score<T>>
    {
        PopulationTrainer Trainer { get; }

        public PopulationEvaluatorTrainingScore(PopulationTrainer trainer)
        {
            Trainer = trainer;
        }

        public List<Score<T>> Evaluate(List<Individual<T>> pop)
        {
            var scores = Trainer.Train(pop.Select(ind => ind.Weights).ToList());
            return pop.Zip(scores, (ind, s) => new Score<T>(ind, s)).ToList();
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
        public bool IsParallel { get; }

        public PopulationTrainer(int depth, int endStage, int numGames, bool isParallel)
        {
            Depth = depth;
            EndStage = endStage;
            NumGames = numGames;
            IsParallel = isParallel;
        }

        public PlayerAI CreatePlayer(Evaluator e)
        {
            return new PlayerAI(e)
            {
                Params = new[] { new SearchParameters(stage: 0, type: SearchType.Normal, depth: Depth),
                                              new SearchParameters(stage: EndStage, type: SearchType.Normal, depth: 64)},
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
        public PopulationTrainerCoLearning(int depth, int endStage, int numGames, bool isParallel) : base(depth, endStage, numGames, isParallel)
        {
        }

        public override List<float> Train(List<Weight> pop)
        {
            var evaluator = new EvaluatorRandomChoice(pop.Select(p => new EvaluatorWeightsBased(p)).ToArray());
            Player player = CreatePlayer(evaluator);

            foreach (var w in pop)
                w.Reset();

            var trainers = pop.Select(p => new Trainer(p, 0.001F)).ToArray();

            for (int i = 0; i < NumGames / 16; i++)
            {
                var data = TrainerUtil.PlayForTrainingParallel(16, player);

                Parallel.ForEach(trainers, trainer => data.ForEach(t => trainer.Update(t.board, t.result)));

                // Console.WriteLine($"{i} / {NumGames / 16}");
            }

            return trainers.Select(trainer => trainer.Log.TakeLast(trainer.Log.Count / 4).Average()).ToList();
        }
    }

    public class PopulationTrainerSelfPlay : PopulationTrainer
    {
        public PopulationTrainerSelfPlay(int depth, int endStage, int numGames, bool isParallel) : base(depth, endStage, numGames, isParallel)
        {
        }

        public override List<float> Train(List<Weight> pop)
        {
            if (IsParallel)
            {
                return pop.AsParallel().Select(Train).ToList();
            }
            else
            {

                return pop.Select(Train).ToList();
            }
        }

        public float Train(Weight w)
        {
            var rand = new Random();
            var player = CreatePlayer(new EvaluatorWeightsBased(w));
            var trainer = new Trainer(w, 0.002F);

            for (int i = 0; i < NumGames / 16; i++)
            {
                var data = TrainerUtil.PlayForTraining(16, player, rand);
                data.ForEach(t => trainer.Update(t.board, t.result));
            }

            return trainer.Log.TakeLast(trainer.Log.Count / 4).Average();
        }
    }
}
