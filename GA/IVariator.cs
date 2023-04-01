using System;
using System.Collections.Generic;
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
        public int Size { get; }

        protected VariationGroup(int size)
        {
            Size = size;
        }

        public abstract Individual<T> Generate(Individual<T>[] elites, Individual<T>[] non_elites, Random rand);
    }

    public class VariationGroupOperation1<T> : VariationGroup<T>
    {
        public IGeneticOperator1<T> Operator { get; }

        public VariationGroupOperation1(IGeneticOperator1<T> opt, int size) : base(size)
        {
            Operator = opt;
        }

        public override Individual<T> Generate(Individual<T>[] elites, Individual<T>[] non_elites, Random rand)
        {
            return Operator.Operate(rand.Choice(elites), rand);
        }
    }

    public class VariationGroupOperation2<T> : VariationGroup<T>
    {
        public IGeneticOperator2<T> Operator { get; }

        public VariationGroupOperation2(IGeneticOperator2<T> opt, int size) : base(size)
        {
            Operator = opt;
        }

        public override Individual<T> Generate(Individual<T>[] elites, Individual<T>[] non_elites, Random rand)
        {
            return Operator.Operate(rand.Choice(elites), rand.Choice(non_elites), rand);
        }
    }

    public class VariationGroupRandom<T> : VariationGroup<T>
    {
        public VariationGroupRandom(int size) : base(size)
        {
        }

        public override Individual<T> Generate(Individual<T>[] elites, Individual<T>[] non_elites, Random rand)
        {
            return elites[0].Info.Generate(rand);
        }
    }

    public class VariatorEliteArchive<T> : IVariator<T, Score<T>>
    {
        public int NumElites { get; set; }

        public VariationGroup<T>[] Groups { get; set; }

        public List<Individual<T>> Vary(List<Score<T>> score, int gen, Random rand)
        {
            var ordered = score.OrderBy(s => s.score).Select(s => s.ind).ToArray();

            var elites = ordered.Take(NumElites).ToArray();
            var non_elites = ordered.Skip(NumElites).ToArray();

            var result = new HashSet<Individual<T>>(elites);

            foreach (var group in Groups)
            {
                int n_init = result.Count;

                while (result.Count - n_init < group.Size)
                    result.Add(group.Generate(elites, non_elites, rand));
            }

            return result.ToList();
        }
    }

    public interface IMigrationModel
    {
        int[] CreateMigrationTable(int gen, int n_dimes);
    }

    public class MigrationModelLine : IMigrationModel
    {
        Random Random { get; } = new Random();
        public int Range { get; set; }

        public int[] CreateMigrationTable(int gen, int n_dimes)
        {
            return n_dimes.Loop(i =>
            {
                int min = Math.Clamp(i - Range, 0, n_dimes - 1);
                int max = Math.Clamp(i + Range, 0, n_dimes - 1);

                int idx = Random.Next(min, max);

                if (idx == i)
                    idx = max;

                return idx;
            }).ToArray();
        }
    }

    public class VariatorDistributed<T> : IVariator<T, Score<T>>
    {
        public int MigrationRate { get; init; }
        public int NumDime { get; init; }

        public IVariator<T, Score<T>> Variator { get; init; }

        public int MigrationSize { get; init; }
        public IMigrationModel MigrationModel { get; init; }

        public List<Individual<T>> Vary(List<Score<T>> score, int gen, Random rand)
        {
            if (NumDime == 1)
                return Variator.Vary(score, gen, rand);

            var result = new List<Individual<T>>();

            int size = score.Count / NumDime;

            var dimes = Enumerable.Range(0, NumDime)
                .Select(i => Variator.Vary(score.Skip(i * size).Take(size).ToList(), gen, rand)).ToArray();

            if (gen % MigrationRate == 0 && gen > 0)
            {
                int[] table = MigrationModel.CreateMigrationTable(gen, NumDime);

                for (int i = 0; i < NumDime; i++)
                {
                    var migrations = dimes[table[i]].Take(MigrationSize).ToList();
                    var next = dimes[i].Take(dimes[i].Count - migrations.Count).Concat(migrations).ToList();

                    result.AddRange(next);
                }
            }
            else
            {
                for (int i = 0; i < NumDime; i++)
                {
                    result.AddRange(dimes[i]);
                }
            }

            if (result.Count < size * NumDime)
            {
                result.AddRange(result.TakeLast(size * NumDime - result.Count).ToList());
            }

            return result.ToList();
        }
    }
}
