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
        public List<ScoreNSGA2> Select(List<ScoreNSGA2> scores, int n, Random rand)
        {
            return scores.OrderBy(s => s.rank).ThenBy(s => s.congestion).Take(n).ToList();
        }
    }
}
