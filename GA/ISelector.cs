using System;
using System.Collections.Generic;
using System.Linq;

namespace OthelloAI.GA
{
    public interface ISelector<T, U> where U : Score<T>
    {
        public U Select(List<U> scores, Random rand);
        public (U, U) SelectPair(List<U> scores, Random rand);
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

        public U Select(List<U> scores, Random rand)
        {
            return Enumerable.Range(0, TournamentSize).Select(_ => rand.Choice(scores)).MinBy(Score).First();
        }

        public (U, U) SelectPair(List<U> scores, Random rand)
        {
            return (Select(scores, rand), Select(scores, rand));
        }
    }

    public class SelectorEliteBiased<T> : ISelector<T, ScoreElite<T>>
    {
        public ScoreElite<T> Select(List<ScoreElite<T>> scores, Random rand)
        {
            throw new NotImplementedException();
        }

        public (ScoreElite<T>, ScoreElite<T>) SelectPair(List<ScoreElite<T>> scores, Random rand)
        {
            var elite = rand.Choice(scores.Where(s => s.is_elite).ToList());
            var non_elite = rand.Choice(scores.Where(s => !s.is_elite).ToList());

            return (elite, non_elite);
        }
    }

    public interface INaturalSelector<T, U> where U : Score<T>
    {
        public List<U> Select(List<U> individuals, int n, Random rand);
    }

    public class NaturalSelector<T, U> : INaturalSelector<T, U> where U : Score<T>
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

    public class NaturalSelectorNSGA2<T> : INaturalSelector<T, ScoreNSGA2<T>>
    {
        public List<ScoreNSGA2<T>> Select(List<ScoreNSGA2<T>> scores, int n, Random rand)
        {
            return scores.OrderBy(s => s.rank).ThenBy(s => s.congestion).Take(n).ToList();
        }
    }
}
