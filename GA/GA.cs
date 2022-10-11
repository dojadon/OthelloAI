using OthelloAI.Patterns;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;

namespace OthelloAI.GA
{
    public class Score<T>
    {
        public Individual<T> ind;
        public float score;

        public Score(Individual<T> ind, float score)
        {
            this.ind = ind;
            this.score = score;
        }
    }

    public class Score2D<T> : Score<T>
    {
        public float score2;

        public Score2D(Individual<T> ind, float score1, float score2) : base(ind, score1)
        {
            this.score2 = score2;
        }
    }

    public class ScoreNSGA2<T> : Score2D<T>
    {
        public float congestion;
        public int rank;

        public ScoreNSGA2(Individual<T> ind, float score1, float score2, int rank, float congestion) : base(ind, score1, score2)
        {
            this.congestion = congestion;
            this.rank = rank;
        }
    }

    public class ScoreElite<T> : Score<T>
    {
        public bool is_elite;

        public ScoreElite(Individual<T> ind, float score, bool is_elite) : base(ind, score)
        {
            this.is_elite = is_elite;
        }
    }

    public class GenomeInfo<T>
    {
        public Func<T> GenomeGenerator { get; set; }
        public Func<T, int, ulong> Decoder { get; set; }
        public Func<T[], T> Combiner { get; set; }

        public int NumTuple { get; set; }
        public int SizeMax { get; set; }
        public int SizeMin { get; set; }

        public Individual<T> Generate(Random rand)
        {
            int n = (int) Math.Pow(3, SizeMax - SizeMin);

            GenomeGroup<T> CreateGenome()
            {
                return new GenomeGroup<T>(Enumerable.Range(0, n).Select(_ => GenomeGenerator()).ToArray(), rand.Next(SizeMin, SizeMax + 1));
            }

            return new Individual<T>(Enumerable.Range(0, NumTuple).Select(_ => CreateGenome()).ToArray(), this);
        }
    }

    public class IndividualIO<T>
    {
        public Action<T, BinaryWriter> WriteGenome { get; set; }
        public Func<BinaryReader, T> ReadGenome { get; set; }

        public Func<T, int, ulong> Decoder { get; set; }

        public void Write(Individual<T> ind, BinaryWriter writer)
        {
            writer.Write(ind.Genome.Length);

            // foreach (var g in ind.Genome)
            // WriteGenome(g, writer);
        }

        public void Write(List<Individual<T>> pop, BinaryWriter writer)
        {
            writer.Write(pop.Count);

            foreach (var ind in pop)
                Write(ind, writer);
        }

        public List<Individual<T>> Read(BinaryReader reader)
        {
            int n = reader.ReadInt32();
            var result = new List<Individual<T>>();

            for (int i = 0; i < n; i++)
            {
                T[] gene = new T[reader.ReadInt32()];

                for (int j = 0; j < gene.Length; j++)
                    gene[j] = ReadGenome(reader);

                // result.Add(new Individual<T>(gene, Decoder));
            }

            return result;
        }

        public void Save(string file, List<Individual<T>> pop)
        {
            using var writer = new BinaryWriter(new FileStream(file, FileMode.Create));
            Write(pop, writer);
        }

        public List<Individual<T>> Load(string file)
        {
            using var reader = new BinaryReader(new FileStream(file, FileMode.Open));
            return Read(reader);
        }
    }

    public class GATest
    {
        private static ThreadLocal<Random> ThreadLocalRandom { get; } = new ThreadLocal<Random>(() => new Random());
        public static Random Random => ThreadLocalRandom.Value;

        public static void TestBRKGA()
        {
            static ulong Decode(float[] keys, int size)
            {
                var indices = keys.Select((k, i) => (k, i)).OrderBy(t => t.k).Select(t => t.i).Take(size);

                ulong g = 0;
                foreach (var i in indices)
                    g |= 1UL << i;
                return g;
            }

            static float[] Combine(float[][] g)
            {
                return Enumerable.Range(0, g[0].Length).Select(i => g.Select(a => a[i]).Min()).ToArray();
            }

            var info = new GenomeInfo<float[]>()
            {
                NumTuple = 10,
                SizeMin = 2,
                SizeMax = 3,
                GenomeGenerator = () => Enumerable.Range(0, 19).Select(_ => (float)Random.NextDouble()).ToArray(),
                Decoder = Decode,
                Combiner = Combine,
            };

            var ga = new GA<float[], Score<float[]>>()
            {
                Info = info,
                Evaluator = new PopulationEvaluatorTrainingScore<float[]>(new PopulationTrainerCoLearning(1, 54, 1600, true)),
                Variator = new VariatorEliteArchive<float[]>()
                {
                    NumElites = 20,
                    NumCx = 70,
                    NumMutants = 10,
                    Crossover = new CrossoverEliteBiased(0.7F),
                    Generator = info,
                },

                IO = new IndividualIO<float[]>()
                {
                    Decoder = Decode,
                    ReadGenome = reader =>
                    {
                        var gene = new float[reader.ReadInt32()];
                        for (int i = 0; i < gene.Length; i++)
                            gene[i] = reader.ReadSingle();
                        return gene;
                    },
                    WriteGenome = (gene, writer) =>
                    {
                        writer.Write(gene.Length);
                        Array.ForEach(gene, writer.Write);
                    }
                },
            };

            var log = $"ga/log_{DateTime.Now:yyyy_MM_dd_HH_mm}.csv";

            ga.Run(ga.Init(100), (n_gen, pop) =>
            {
                var score = pop.MinBy(ind => ind.score).First();

                using (StreamWriter sw = File.AppendText(log))
                {
                    foreach (var s in pop.OrderBy(s => s.score))
                    {
                        sw.WriteLine(s.score + ", " + string.Join(", ",  s.ind.Tuples.SelectMany(t => t.Tuples)));
                    }
                    // sw.WriteLine(string.Join(", ", pop.Select(ind => ind.score).OrderBy(s => s)));
                }

                ulong[] tuples = score.ind.Tuples.SelectMany(t => t.Tuples).ToArray();

                foreach(var s in pop)
                {
                    Console.WriteLine(s.ind.Tuples.Select(t => t.NumTuples).Sum());
                }

                for (int i = 0; i < tuples.Length / 2.0F; i++)
                {
                    var b1 = tuples[i * 2];
                    var b2 = tuples[i * 2 + 1];

                    Console.WriteLine(new Board(b1, Board.HorizontalMirror(b2)));
                }

                Console.WriteLine($"Gen : {n_gen}");
                Console.WriteLine(score.score);
                Console.WriteLine(score.ind.Tuples.Select(t => t.NumTuples).Sum());
                Console.WriteLine(string.Join(", ", score.ind.Tuples.Select(t => t.Size)));
            });
        }
    }

    public class GA<T, U> where U : Score<T>
    {
        private static ThreadLocal<Random> ThreadLocalRandom { get; } = new ThreadLocal<Random>(() => new Random());

        public GenomeInfo<T> Info { get; set; }
        public IndividualIO<T> IO { get; set; }

        public ISelector<T, U> Selecter { get; set; }
        public INaturalSelector<T, U> NaturalSelector { get; set; }
        public GeneticOperators<T, U> GeneticOperator { get; set; }
        public IPopulationEvaluator<T, U> Evaluator { get; set; }
        public IVariator<T, U> Variator { get; set; }

        public static Random Random => ThreadLocalRandom.Value;

        public static string ConcatHorizontal(string s1, string s2, int margin)
        {
            var a1 = s1.Split(Environment.NewLine);
            var a2 = s2.Split(Environment.NewLine);

            int max = a1.Concat(a2).Max(s => s.Length);
            int len = max + margin;

            return a1.Zip(a2, (b1, b2) => b1.PadRight(len) + b2).Aggregate((b1, b2) => b1 + Environment.NewLine + b2);
        }

        public void Run(List<Individual<T>> pop, Action<int, List<U>> logger)
        {
            for (int i = 0; i < 10000; i++)
            {
                Console.WriteLine("Evaluate");
                var scores = Evaluator.Evaluate(pop);

                logger(i, scores);

                pop = Variator.Vary(scores, Random);
                // pop = pop.Distinct(Generator).ToList();

                IO.Save("ga/ind.dat", pop);
            }
        }

        public List<Individual<T>> Init(int n_pop)
        {
            return Enumerable.Range(0, n_pop).Select(_ => Info.Generate(Random)).ToList();
        }
    }

    public class GenomeGroup<T>
    {
        public T[] Genome { get; }
        public int Size { get; }

        public GenomeGroup(T[] genome, int size)
        {
            Genome = genome;
            Size = size;
        }
    }

    public class Tuple<T>
    {
        public T[] Genome { get; }
        public ulong[] Tuples { get; }
        public Pattern[] Patterns { get; }

        public GenomeInfo<T> Info { get; }

        public int Size { get; }
        public int NumTuples { get; }

        public Tuple(T[] genome, int size, GenomeInfo<T> info)
        {
            Genome = genome;
            Size = size;
            Info = info;

            NumTuples = (int)Math.Pow(3, info.SizeMax - Size);
            Tuples = Enumerable.Range(0, NumTuples).Select(i =>
            {
                int n = genome.Length / NumTuples;
                T g = info.Combiner(genome.Skip(n * i).Take(n).ToArray());
                return info.Decoder(g, Size);
            }).ToArray();

            Patterns = Tuples.Select(g => new Pattern($"ga/{g}.dat", 10, new BoardHasherMask(g), PatternType.ASYMMETRIC)).ToArray();
        }

        public override bool Equals(object obj)
        {
            if (obj is Tuple<T> t)
                return Equals(t);

            return false;
        }

        public bool Equals(Tuple<T> y)
        {
            if (ReferenceEquals(this, y))
                return true;

            for (int i = 0; i < Tuples.Length; i++)
                if (Tuples[i] != y.Tuples[i])
                    return false;

            return true;
        }

        public override int GetHashCode()
        {
            return Tuples.Aggregate(0, (total, next) => HashCode.Combine(total, next));
        }
    }

    public class Individual<T>
    {
        public GenomeGroup<T>[] Genome { get; }
        public Tuple<T>[] Tuples { get; }

        public GenomeInfo<T> Info { get; }

        public List<float> Log { get; } = new List<float>();

        public Individual(GenomeGroup<T>[] genome, GenomeInfo<T> info)
        {
            Genome = genome;
            Info = info;

            Tuples = Genome.Select(g => new Tuple<T>(g.Genome, g.Size, info)).ToArray();
        }

        public Pattern[] GetPatterns() => Tuples.SelectMany(t => t.Patterns).ToArray();

        public Evaluator CreateEvaluator() => new EvaluatorPatternBased(GetPatterns());

        public override bool Equals(object obj)
        {
            if (obj is Individual<T> ind)
                return Equals(ind);

            return false;
        }

        public bool Equals(Individual<T> y)
        {
            if (ReferenceEquals(this, y))
                return true;

            for (int i = 0; i < Tuples.Length; i++)
                if (Tuples[i] != y.Tuples[i])
                    return false;

            return true;
        }

        public override int GetHashCode()
        {
            return Tuples.Aggregate(0, (total, next) => HashCode.Combine(total, next));
        }

        public static (int, int)[] ClosestPairs(T[] g1, T[] g2, Func<T, T, float> distance)
        {
            int n_gene = g1.Length;

            var pairs = Enumerable.Range(0, n_gene).SelectMany(i => Enumerable.Range(0, n_gene).Select(j => (i, j))).OrderBy(t => distance(g1[t.i], g2[t.j])).ToList();
            var added1 = new List<int>();
            var added2 = new List<int>();

            var result = new List<(int, int)>();

            foreach ((int i, int j) in pairs)
            {
                if (added1.Contains(i) || added2.Contains(j))
                    continue;

                added1.Add(i);
                added2.Add(j);

                result.Add((i, j));
            }

            return result.ToArray();
        }
    }
}
