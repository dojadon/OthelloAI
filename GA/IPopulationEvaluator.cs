using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OthelloAI.GA
{
    public interface IPopulationEvaluator<T> where T : Score
    {
        public List<T> Evaluate(List<Individual> pop);
    }

    public class PopulationEvaluatorRandomTournament : IPopulationEvaluator<Score>
    {
        public int Depth { get; }
        public int EndStage { get; }
        public int NumGames { get; }

        public PopulationEvaluatorRandomTournament(int depth, int endStage, int n_games)
        {
            Depth = depth;
            EndStage = endStage;
            NumGames = n_games;
        }

        public PlayerAI CreatePlayer(Individual ind)
        {
            return new PlayerAI(ind.CreateEvaluator())
            {
                ParamBeg = new SearchParameters(depth: Depth, stage: 0, new CutoffParameters(true, true, false)),
                ParamMid = new SearchParameters(depth: Depth, stage: 16, new CutoffParameters(true, true, false)),
                ParamEnd = new SearchParameters(depth: 64, stage: EndStage, new CutoffParameters(true, true, false)),
                PrintInfo = false,
            };
        }

        public static (int, int)[] Combinations(int n)
        {
            return Enumerable.Range(0, n - 1).SelectMany(i => Enumerable.Range(i + 1, n - i - 1).Select(j => (i, j))).ToArray();
        }

        public List<Score> Evaluate(List<Individual> pop)
        {
            foreach (var ind in pop)
            {
                ind.Score = 0;
                ind.Log.Clear();
            }

            int count = 0;

            Parallel.For(0, NumGames, i =>
            {
                var rand = new Random();
                var pair = rand.SamplePair(pop.Count);

                var ind1 = pop[pair.Item1];
                var ind2 = pop[pair.Item2];

                PlayerAI p1 = CreatePlayer(ind1);
                PlayerAI p2 = CreatePlayer(ind2);

                Board b = Tester.PlayGame(Tester.CreateRnadomGame(rand, 6), p1, p2);
                int result = b.GetStoneCountGap();

                Interlocked.Increment(ref count);
                Console.WriteLine($"{count} / {NumGames} : {pair.Item1} vs {pair.Item2} : {result}");

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

            return pop.Select(ind => new Score(ind, ind.Log.Average())).ToList();
        }
    }

    public class PopulationEvaluatorTournament : IPopulationEvaluator<Score>
    {
        public int Depth { get; }
        public int EndStage { get; }

        public PopulationEvaluatorTournament(int depth, int endStage)
        {
            Depth = depth;
            EndStage = endStage;
        }

        public PlayerAI CreatePlayer(Individual ind)
        {
            return new PlayerAI(ind.CreateEvaluator())
            {
                ParamBeg = new SearchParameters(depth: Depth, stage: 0, new CutoffParameters(true, true, false)),
                ParamMid = new SearchParameters(depth: Depth, stage: 16, new CutoffParameters(true, true, false)),
                ParamEnd = new SearchParameters(depth: 64, stage: EndStage, new CutoffParameters(true, true, false)),
                PrintInfo = false,
            };
        }

        public static (int, int)[] Combinations(int n)
        {
            return Enumerable.Range(0, n - 1).SelectMany(i => Enumerable.Range(i + 1, n - i - 1).Select(j => (i, j))).ToArray();
        }

        public List<Score> Evaluate(List<Individual> pop)
        {
            foreach (var ind in pop)
            {
                ind.Score = 0;
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
                        b = Tester.PlayGame(Board.Init, p1, p2);
                        result = b.GetStoneCountGap();
                    }
                    else
                    {
                        b = Tester.PlayGame(Board.Init, p2, p1);
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

            return pop.Select(ind => new Score(ind, ind.Log.Average())).ToList();
        }
    }

    public class PopulationEvaluatorTrainingScore : IPopulationEvaluator<Score>
    {
        PopulationTrainer Trainer { get; }

        public PopulationEvaluatorTrainingScore(PopulationTrainer trainer)
        {
            Trainer = trainer;
        }

        public List<Score> Evaluate(List<Individual> pop)
        {
            return Trainer.Train(pop);
        }
    }

    public class PopulationEvaluatorNobeilty : IPopulationEvaluator<Score>
    {
        public List<Score> Evaluate(List<Individual> pop)
        {
            throw new NotImplementedException();
        }
    }

    public class PopulationEvaluatorNSGA2 : IPopulationEvaluator<ScoreNSGA2>
    {
        IPopulationEvaluator<Score> Evaluator1 { get; }
        IPopulationEvaluator<Score> Evaluator2 { get; }

        public List<(Score2D s, float c)> CalcCongection(List<Score2D> scores)
        {
            static float Distance(Score2D i1, Score2D i2)
            {
                float s1 = i1.score - i2.score;
                float s2 = i1.score2 - i2.score2;
                return (float) Math.Sqrt(s1 * s1 + s2 * s2);
            }

            var ordered = scores.OrderBy(s => s.score).ToList();

            var dist = ordered.Take(scores.Count - 1).Zip(ordered.Skip(1), Distance).ToArray();

            var c0 = new float[] { dist.Max() };
            var c1 = dist.Concat(c0);
            var c2 = c0.Concat(dist);

            return scores.ZipThree(c1, c2, (s, f1, f2) => (s, f1 + f2)).ToList();
        }

        public List<ScoreNSGA2> NonDominatedSort(List<Score2D> scores)
        {
            bool Dominated(Score2D s1, Score2D s2)
            {
                return s1.score > s2.score && s1.score2 > s2.score2;
            }

            bool IsNonDominated(Score2D s)
            {
                return scores.Count(s2 => Dominated(s, s2)) == 0;
            }

            var scoreNSGA2 = new List<ScoreNSGA2>();
            int rank = 0;

            while (scores.Count > 0)
            {
                var rank0 = scores.Where(IsNonDominated).ToList();
                scores.RemoveAll(i => rank0.Contains(i));

                var cong =  CalcCongection(rank0);
                scoreNSGA2.AddRange(cong.Select(t => new ScoreNSGA2(t.s.ind, t.s.score, t.s.score2, rank, -t.c)));

                rank++;
            }

            return scoreNSGA2;
        }

        public List<ScoreNSGA2> Evaluate(List<Individual> pop)
        {
            var score1 = Evaluator1.Evaluate(pop);
            var score2 = Evaluator2.Evaluate(pop);

            var score = score1.Zip(score2).Select(t => new Score2D(t.First.ind, t.First.score, t.Second.score)).ToList();

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
                ParamBeg = new SearchParameters(depth: Depth, stage: 0, new CutoffParameters(true, true, false)),
                ParamMid = new SearchParameters(depth: Depth, stage: 16, new CutoffParameters(true, true, false)),
                ParamEnd = new SearchParameters(depth: 64, stage: EndStage, new CutoffParameters(true, true, false)),
                PrintInfo = false,
            };
        }

        public PlayerAI CreatePlayer(Individual ind)
        {
            return CreatePlayer(ind.CreateEvaluator());
        }

        public abstract List<Score> Train(List<Individual> pop);
    }

    public class PopulationTrainerCoLearning : PopulationTrainer
    {
        public PopulationTrainerCoLearning(int depth, int endStage, int numGames, bool isParallel) : base(depth, endStage, numGames, isParallel)
        {
        }

        public override List<Score> Train(List<Individual> pop)
        {
            var evaluator = new EvaluatorRandomChoice(pop.Select(i => i.CreateEvaluator()).ToArray());
            Player player = CreatePlayer(evaluator);

            for (int i = 0; i < NumGames / 16; i++)
            {
                var data = TrainerUtil.PlayForTrainingParallel(16, player);

                Parallel.ForEach(pop, ind =>
                {
                    float e = data.Select(t => ind.Trainer.Update(t.board, t.result)).Select(f => f * f).Average();
                    ind.Log.Add(e);
                });

                // Console.WriteLine($"{i} / {NumGames / 16}");
            }

            return pop.Select(ind => new Score(ind, ind.Log.TakeLast(ind.Log.Count / 4).Average())).ToList();
        }
    }

    public class PopulationTrainerSelfPlay : PopulationTrainer
    {
        public PopulationTrainerSelfPlay(int depth, int endStage, int numGames, bool isParallel) : base(depth, endStage, numGames, isParallel)
        {
        }

        public override List<Score> Train(List<Individual> pop)
        {
            if (IsParallel)
            {
                return pop.AsParallel().Select(ind => new Score(ind, Train(ind))).ToList();
            }
            else
            {

                return pop.Select(ind => new Score(ind, Train(ind))).ToList();
            }
        }

        public float Train(Individual ind)
        {
            var rand = new Random();
            PlayerAI player = CreatePlayer(ind);

            for (int i = 0; i < NumGames / 16; i++)
            {
                var data = TrainerUtil.PlayForTraining(16, player, rand, false);
                float e = data.Select(t => ind.Trainer.Update(t.board, t.result)).Select(f => f * f).Average();
                ind.Log.Add(e);
            }

            return ind.Log.TakeLast(ind.Log.Count / 4).Average();
        }
    }
}
