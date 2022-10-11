using System;
using System.Collections.Generic;
using System.Linq;

namespace OthelloAI.GA
{
    public interface IVariator<T, U> where U : Score<T>
    {
        public List<Individual<T>> Vary(List<U> score, Random rand);
    }

    public class VariatorEliteArchive<T> : IVariator<T, Score<T>>
    {
        public int NumElites { get; set; }
        public int NumMutants { get; set; }
        public int NumCx { get; set; }

        public GenomeInfo<T> Generator { get; set; }

        public IGeneticOperator2<T> Crossover { get; set; }

        public List<Individual<T>> Vary(List<Score<T>> score, Random rand)
        {
            var ordered = score.OrderBy(s => s.score).Select(s => s.ind).ToArray();

            var elites = ordered.Take(NumElites).ToList();
            var non_elites = ordered.Skip(NumElites).ToList();

            var result = new HashSet<Individual<T>>(elites);

            while (result.Count < NumElites + NumCx)
                result.Add(Crossover.Operate(rand.Choice(elites), rand.Choice(non_elites), rand));

            var mutants = Enumerable.Range(0, NumMutants).Select(_ => Generator.Generate(rand));

            return result.Concat(mutants).ToList();
        }
    }
}
