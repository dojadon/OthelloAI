using System;
using System.Collections.Generic;
using System.Linq;

namespace OthelloAI.GA
{
    public interface IVariator<T, U> where U : Score<T>
    {
        public List<Individual<T>> Vary(List<U> score, Random rand);
    }

    public class VariatorES<T> : IVariator<T, Score<T>>
    {
        public int Mu { get; set; }
        public int LambdaM { get; set; }
        public int LambdaCX { get; set; }

        public IGeneticOperator1<T> Mutant { get; set; }
        public IGeneticOperator2<T> Crossover { get; set; }

        public List<Individual<T>> Vary(List<Score<T>> score, Random rand)
        {
            var mu = score.OrderBy(s => s.score).Select(s => s.ind).Take(Mu).ToArray();

            var mutants = Enumerable.Range(0, LambdaM).Select(_ => Mutant.Operate(rand.Choice(mu), rand));
            var crossovers = Enumerable.Range(0, LambdaCX).Select(_ => Crossover.Operate(rand.Choice(mu), rand.Choice(mu), rand));

            return mu.MultiConcat(mutants, crossovers).ToList();
        }
    }

    public class VariatorEliteArchiveNSGA2<T> : IVariator<T, ScoreNSGA2<T>>
    {
        public int NumElites { get; set; }
        public int NumMutants { get; set; }
        public int NumCx { get; set; }

        public GenomeInfo<T> Generator { get; set; }

        public IGeneticOperator2<T> Crossover { get; set; }

        public List<Individual<T>> Vary(List<ScoreNSGA2<T>> score, Random rand)
        {
            var ordered = score.OrderBy(s => s.rank).ThenBy(s => s.congestion).Select(s => s.ind).ToArray();

            var elites = ordered.Take(NumElites).ToList();
            var non_elites = ordered.Skip(NumElites).ToList();

            var result = new HashSet<Individual<T>>(elites);

            while (result.Count < NumElites + NumCx)
                result.Add(Crossover.Operate(rand.Choice(elites), rand.Choice(non_elites), rand));

            var mutants = Enumerable.Range(0, NumMutants).Select(_ => Generator.Generate(rand));

            return result.Concat(mutants).ToList();
        }
    }

    public class VariatorEliteArchive<T> : IVariator<T, Score<T>>
    {
        public int NumElites { get; set; }
        public int NumEliteMutants { get; set; }
        public int NumRandomMutants { get; set; }

        public int NumCx { get; set; }

        public GenomeInfo<T> Generator { get; set; }

        public IGeneticOperator2<T> Crossover { get; set; }
        public IGeneticOperator1<T> MutantElite { get; set; }

        public List<Individual<T>> Vary(List<Score<T>> score, Random rand)
        {
            var ordered = score.OrderBy(s => s.score).Select(s => s.ind).ToArray();

            var elites = ordered.Take(NumElites).ToList();
            var non_elites = ordered.Skip(NumElites).ToList();

            var result = new HashSet<Individual<T>>(elites);

            while (result.Count < NumElites + NumCx)
                result.Add(Crossover.Operate(rand.Choice(elites), rand.Choice(non_elites), rand));

            var mutants_elite = Enumerable.Range(0, NumEliteMutants).Select(_ => MutantElite.Operate(rand.Choice(elites), rand));
            var mutants_random = Enumerable.Range(0, NumRandomMutants).Select(_ => Generator.Generate(rand));

            return result.Concat(mutants_elite).Concat(mutants_random).ToList();
        }
    }
}
