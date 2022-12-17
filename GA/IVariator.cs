using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace OthelloAI.GA
{
    public interface IVariator<T, U> where U : Score<T>
    {
        public List<Individual<T>> Vary(List<U> score, int gen, Random rand);
    }

    public class VariatorES<T> : IVariator<T, Score<T>>
    {
        public int Mu { get; set; }
        public int LambdaM { get; set; }
        public int LambdaCX { get; set; }

        public IGeneticOperator1<T> Mutant { get; set; }
        public IGeneticOperator2<T> Crossover { get; set; }

        public List<Individual<T>> Vary(List<Score<T>> score, int gen, Random rand)
        {
            var mu = score.OrderBy(s => s.score).Select(s => s.ind).Take(Mu).ToArray();

            var result = new HashSet<Individual<T>>(mu);

            for (int i = 0; i < LambdaCX; i++)
            {
                result.Add(Crossover.Operate(rand.Choice(mu), rand.Choice(mu), rand));
            }

            while (result.Count < Mu + LambdaM)
            {
                result.Add(Mutant.Operate(rand.Choice(mu), rand));
            }

            return result.ToList();
        }
    }

    public class VariatorEliteArchiveNSGA2<T> : IVariator<T, ScoreNSGA2<T>>
    {
        public int NumElites { get; set; }
        public int NumMutants { get; set; }
        public int NumCx { get; set; }

        public GenomeInfo<T> Generator { get; set; }

        public IGeneticOperator2<T> Crossover { get; set; }

        public List<Individual<T>> Vary(List<ScoreNSGA2<T>> score, int gen, Random rand)
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

    public abstract class VariationGroup<T>
    {
        public IGeneticOperator2<T> Operator { get; }
        public int Size { get; }
        public int MigrationSize { get; }

        public VariationGroup(IGeneticOperator2<T> opt, int size, int migrationSize)
        {
            Operator = opt;
            Size = size;
            MigrationSize = migrationSize;
        }

        public VariationGroup(IGeneticOperator1<T> opt, int size, int migrationSize) : this(new CrossoverMutant<T>(opt), size, migrationSize)
        {
        }

        public abstract Individual<T>[] Generate(Individual<T>[] elites, Individual<T>[] non_elites);
    }

    public class VariationGroupElite<T> : VariationGroup<T>
    {
        public override Individual<T> Generate(Individual<T>[] elites, Individual<T>[] non_elites)
        {
            throw new NotImplementedException();
        }
    }

    public class VariatorEliteArchive<T> : IVariator<T, Score<T>>
    {
        public int NumElites { get; set; }

        public VariationGroup<T>[] Groups { get; set; }

        public List<Individual<T>> Vary(List<Score<T>> score, int gen, Random rand)
        {
            var ordered = score.OrderBy(s => s.score).Select(s => s.ind).ToArray();

            var elites = ordered.Take(NumElites).ToList();
            var non_elites = ordered.Skip(NumElites).ToList();

            var result = new HashSet<Individual<T>>(elites);

            foreach(var group in Groups)
            {
                int n_init = result.Count;

                while (result.Count - n_init < group.Size)
                    result.Add(group.Operator.Operate(rand.Choice(elites), rand.Choice(non_elites), rand));
            }

            return result.ToList();
        }
    }

    public class VariatorEliteArchiveDistributed<T> : IVariator<T, Score<T>>
    {
        public int MigrationRate { get; set; }
        public int NumDime { get; set; }
        public int NumElites { get; set; }
        public int NumElitesMigration { get; set; }

        public VariationGroup<T>[] Groups { get; set; }

        public int DimeSize => NumElites + Groups.Sum(g => g.Size);

        public List<Individual<T>> Vary(List<Score<T>> score, int gen, Random rand)
        {
            var result = new List<Individual<T>>();

            var dimes = Enumerable.Range(0, NumDime).AsParallel().Select(i => VaryDime(score.Skip(i * DimeSize).Take(DimeSize), rand)).ToArray();

            for (int i = 0; i < NumDime; i++)
            {
                IEnumerable<Individual<T>> next;

                if(gen % MigrationRate == 0)
                {
                    var migrations = dimes[(i + 1) % NumDime].migrations;
                    next = dimes[i].next_gen.Take(DimeSize - migrations.Count).Concat(migrations);
                }
                else
                {
                    next = dimes[i].next_gen;
                }

                result.AddRange(next);
            }

            if(result.Count < DimeSize * NumDime)
            {
                result.AddRange(result.TakeLast(DimeSize * NumDime - result.Count));
            }

            return result.ToList();
        }

        public (List<Individual<T>> next_gen, List<Individual<T>> migrations) VaryDime(IEnumerable<Score<T>> score, Random rand)
        {
            var ordered = score.OrderBy(s => s.score).Select(s => s.ind).ToArray();

            var elites = ordered.Take(NumElites).ToList();
            var non_elites = ordered.Skip(NumElites).ToList();

            var set = new HashSet<Individual<T>>(elites);

            var result = new List<Individual<T>>(elites);
            var migrations = new List<Individual<T>>(elites.Take(NumElitesMigration));

            foreach (var group in Groups)
            {
                var list = new List<Individual<T>>();

                while (list.Count < group.Size)
                {
                    var ind = group.Operator.Operate(rand.Choice(elites), rand.Choice(non_elites), rand);

                    if (set.Contains(ind))
                        continue;

                    set.Add(ind);
                    result.Add(ind);
                    list.Add(ind);
                }

                migrations.AddRange(list.Take(group.MigrationSize));
            }

            return (result.ToList(), migrations);
        }
    }
}
