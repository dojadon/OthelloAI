using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OthelloAI.GA
{
    public interface IPopulationEvaluator
    {
        public void Evaluate(List<Individual> pop);
    }

    public class PopulationEvaluatorRandomTournament : IPopulationEvaluator
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

        public void Evaluate(List<Individual> pop)
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

            foreach (var ind in pop)
            {
                ind.Score = ind.Log.Average();
            }
        }
    }

    public class PopulationEvaluatorTournament : IPopulationEvaluator
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

        public void Evaluate(List<Individual> pop)
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

            foreach (var ind in pop)
            {
                ind.Score = ind.Log.Average();
            }
        }
    }

    public class PopulationEvaluatorTrainingScore : IPopulationEvaluator
    { 
        PopulationTrainer Trainer { get; }

        public PopulationEvaluatorTrainingScore(PopulationTrainer trainer)
        {
            Trainer = trainer;
        }

        public void Evaluate(List<Individual> pop)
        {
            var score = Trainer.Train(pop);

            foreach((var ind, float s) in pop.Zip(score))
            {
                ind.Score = s;
            }
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

        public abstract List<float> Train(List<Individual> pop);
    }

    public class PopulationTrainerCoLearning : PopulationTrainer
    {
        public PopulationTrainerCoLearning(int depth, int endStage, int numGames, bool isParallel) : base(depth, endStage, numGames, isParallel)
        {
        }

        public override List<float> Train(List<Individual> pop)
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

                Console.WriteLine($"{i} / {NumGames / 16}");
            }

            return pop.Select(ind => ind.Log.TakeLast(ind.Log.Count / 4).Average()).ToList();
        }
    }

    public class PopulationTrainerSelfPlay : PopulationTrainer
    {
        public PopulationTrainerSelfPlay(int depth, int endStage, int numGames, bool isParallel) : base(depth, endStage, numGames, isParallel)
        {
        }

        public override List<float> Train(List<Individual> pop)
        {
            if (IsParallel)
            {
                return pop.AsParallel().Select(ind => Train(ind)).ToList();
            }
            else
            {
                return pop.Select(ind => Train(ind)).ToList();
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
