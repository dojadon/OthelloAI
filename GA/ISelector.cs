using System;
using System.Collections.Generic;
using System.Linq;

namespace OthelloAI.GA
{
    public interface ISelector<T> where T : Score
    {
        public T Select(List<T> individuals, Random rand);
    }

    public class SelectorTournament<T> : ISelector<T> where T : Score
    {
        public int TournamentSize { get; set; }
        public Func<T, float> Score { get; }

        public SelectorTournament(int tournamentSize, Func<T, float> score)
        {
            TournamentSize = tournamentSize;
            Score = score;
        }

        public T Select(List<T> individuals, Random rand)
        {
            return Enumerable.Range(0, TournamentSize).Select(_ => rand.Choice(individuals)).MinBy(Score).First();
        }
    }

    public interface INaturalSelector<T> where T : Score
    {
        public List<T> Select(List<T> individuals, int n, Random rand);
    }

    public class NaturalSelector<T> : INaturalSelector<T> where T : Score
    {
        public ISelector<T> Selector { get; }

        public NaturalSelector(ISelector<T> selector)
        {
            Selector = selector;
        }

        public List<T> Select(List<T> individuals, int n, Random rand)
        {
            return Enumerable.Range(0, n).Select(_ => Selector.Select(individuals, rand)).ToList();
        }
    }

    public class NaturalSelectorNSGA2 : INaturalSelector<ScoreNSGA2>
    {
        public List<List<ScoreNSGA2>> NonDominatedSort(List<ScoreNSGA2> population)
        {
            var rankedIndividuals = new List<List<ScoreNSGA2>>();

            bool Dominated(ScoreNSGA2 ind1, ScoreNSGA2 ind2)
            {
                return ind1.score > ind2.score && ind1.score2 > ind2.score2;
            }

            bool IsNonDominated(ScoreNSGA2 ind)
            {
                return population.Count(ind2 => Dominated(ind, ind2)) == 0;
            }

            while (population.Count > 0)
            {
                var rank0 = population.Where(IsNonDominated).ToList();
                population.RemoveAll(i => rank0.Contains(i));
                rankedIndividuals.Add(rank0);
            }

            return rankedIndividuals;
        }

        public List<ScoreNSGA2> Select(List<ScoreNSGA2> pop, int n, Random rand)
        {
            var ranked = NonDominatedSort(pop);

            var p = new List<ScoreNSGA2>();

            for (int i = 0; i < ranked.Count; i++)
            {
                if (p.Count + ranked[i].Count <= n)
                {
                    p.AddRange(ranked[i]);
                }
                else
                {
                    p.AddRange(ranked[i].OrderByDescending(ind => ind.congestion).Take(n - p.Count));
                    break;
                }
            }

            return p;
        }
    }
}
