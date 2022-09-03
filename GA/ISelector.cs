using System;
using System.Collections.Generic;
using System.Linq;

namespace OthelloAI.GA
{
    public interface ISelector<T, U> where U : Score<Individual<T>>
    {
        public U Select(List<U> individuals, Random rand);
    }

    public class SelectorTournament<T, U> : ISelector<T, U> where U : Score<T>
    {
        public int TournamentSize { get; set; }
        public Func<U, float> Score { get; }

        public SelectorTournament(int tournamentSize, Func<U, float> score)
        {
            TournamentSize = tournamentSize;
            Score = score;
        }

        public U Select(List<U> individuals, Random rand)
        {
            return Enumerable.Range(0, TournamentSize).Select(_ => rand.Choice(individuals)).MinBy(Score).First();
        }
    }

    public interface INaturalSelector<T, U> where T : IndividualBase where U : Score<T>
    {
        public List<U> Select(List<U> individuals, int n, Random rand);
    }

    public class NaturalSelector<T, U> : INaturalSelector<T, U> where T : IndividualBase where U : Score<T>
    {
        public ISelector<T, U> Selector { get; }

        public NaturalSelector(ISelector<T, U> selector)
        {
            Selector = selector;
        }

        public List<U> Select(List<U> individuals, int n, Random rand)
        {
            return Enumerable.Range(0, n).Select(_ => Selector.Select(individuals, rand)).ToList();
        }
    }

    public class NaturalSelectorNSGA2<T> : INaturalSelector<T, ScoreNSGA2<T>> where T : IndividualBase
    {
        public List<ScoreNSGA2<T>> Select(List<ScoreNSGA2<T>> scores, int n, Random rand)
        {
            return scores.OrderBy(s => s.rank).ThenBy(s => s.congestion).Take(n).ToList();
        }
    }
}
