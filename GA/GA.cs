using System;
using System.Collections.Generic;
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

        public Func<IEnumerable<T>, float> VarianceT { get; set; }

        public int NumStages { get; set; }
        public int NumTuples { get; set; }
        public int SizeMax { get; set; }
        public int SizeMin { get; set; }

        public int MaxNumWeights { get; set; }

        public Individual<T> Generate(Random rand)
        {
            int n = (int)Math.Pow(3, SizeMax - SizeMin);

            GenomeGroup<T> CreateGenome()
            {
                return new GenomeGroup<T>(GenomeGenerator(), rand.Next(SizeMin, SizeMax + 1));
            }

            return new Individual<T>(Enumerable.Range(0, NumStages).Select(_ => Enumerable.Range(0, NumTuples).Select(_ => CreateGenome()).ToArray()).ToArray(), this);
        }

        public float VarianceG(IEnumerable<GenomeGroup<T>> genomes)
        {
            float v1 = genomes.Select(g => g.Size * 0.1F).Variance();
            float v2 = VarianceT(genomes.Select(g => g.Genome));
            return v1 + v2;
        }

        public float Variance(Individual<T>[] pop)
        {
            return Enumerable.Range(0, NumStages).Select(s => Enumerable.Range(0, NumTuples).Select(t => pop.Select(i => i.Genome[s][t])).Select(VarianceG).Sum()).Sum();
        }
    }

    public class IndividualIO<T>
    {
        public Action<T, BinaryWriter> WriteGenome { get; set; }
        public Func<BinaryReader, T> ReadGenome { get; set; }

        public GenomeInfo<T> Info { get; set; }

        public void Write(Individual<T> ind, BinaryWriter writer)
        {
            writer.Write(ind.Genome.Length);

            foreach (var g1 in ind.Genome)
            {
                writer.Write(g1.Length);

                foreach (var g2 in g1)
                {
                    WriteGenome(g2.Genome, writer);
                    writer.Write(g2.Size);
                }
            }
        }

        public void Write(List<Individual<T>> pop, BinaryWriter writer)
        {
            writer.Write(pop.Count);

            foreach (var ind in pop)
                Write(ind, writer);
        }

        public List<Individual<T>> Read(BinaryReader reader)
        {
            return Enumerable.Range(0, reader.ReadInt32()).Select(_ => ReadIndividual(reader)).ToList();
        }

        public Individual<T> ReadIndividual(BinaryReader reader)
        {
            GenomeGroup<T>[][] gene = new GenomeGroup<T>[reader.ReadInt32()][];

            for (int i = 0; i < gene.Length; i++)
            {
                gene[i] = new GenomeGroup<T>[reader.ReadInt32()];

                for (int j = 0; j < gene[i].Length; j++)
                {
                    var g = ReadGenome(reader);
                    int size = reader.ReadInt32();

                    gene[i][j] = new GenomeGroup<T>(g, size);
                }
            }

            return new Individual<T>(gene, Info);
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

        public static void TestES()
        {
            var info = new GenomeInfo<ulong>()
            {
                NumStages = 1,
                NumTuples = 2,
                SizeMin = 8,
                SizeMax = 8,
                MaxNumWeights = (int)Math.Pow(3, 8) * 2,
                GenomeGenerator = () => Random.GenerateRegion(19, 8),
                Decoder = (g, _) => g,
                VarianceT = _ => 0,
            };

            var ga = new GA<ulong, Score<ulong>>()
            {
                Info = info,
                Evaluator = new PopulationEvaluatorRandomTournament<ulong>(new PopulationTrainerCoLearning(1, 54, 6400, true), 2, 54, 100 * 400)
                {
                    GetDepthFraction = (_, _, _) => (1, 1)
                },
                // Evaluator = new PopulationEvaluatorTrainingScore<float[]>(new PopulationTrainerCoLearning(1, 54, 3200, true)),
                Variator = new VariatorES<ulong>()
                {
                    Mu = 10,
                    LambdaM = 80,
                    LambdaCX = 10,
                    Mutant = new MutantBits(),
                    Crossover = new CrossoverExchange<ulong>(),
                },

                IO = new IndividualIO<ulong>()
                {
                    Info = info,
                    ReadGenome = reader => reader.ReadUInt64(),
                    WriteGenome = (gene, writer) => writer.Write(gene)
                },
            };

            var log = $"ga/log_{DateTime.Now:yyyy_MM_dd_HH_mm}.csv";
            using StreamWriter sw = File.AppendText(log);

            // ga.Run(ga.IO.Load("ga/ind.dat"), (n_gen, time, pop) =>
            ga.Run(ga.Init(100), (n_gen, time, pop) =>
            {
                var score = pop.MinBy(ind => ind.score).First();

                foreach (var s in pop.OrderBy(s => s.score))
                {
                    sw.Write(s.score + ",");

                    foreach (var t in s.ind.Tuples)
                    {
                        sw.Write(",," + string.Join(", ", t.Select(t => t.TupleBit)));
                    }
                    sw.WriteLine();
                }

                foreach (var t in score.ind.Tuples)
                {
                    ulong[] tuples = t.Select(t => t.TupleBit).ToArray();

                    for (int i = 0; i < tuples.Length / 2.0F; i++)
                    {
                        var b1 = tuples[i * 2];

                        if (i * 2 + 1 < tuples.Length)
                        {
                            var b2 = tuples[i * 2 + 1];
                            Console.WriteLine(new Board(b1, Board.HorizontalMirror(b2)));
                        }
                        else
                        {
                            Console.WriteLine(new Board(b1, 0));
                        }
                    }
                    Console.WriteLine();
                }

                Console.WriteLine($"Gen : {n_gen}, {time}");
                Console.WriteLine(string.Join(", ", score.ind.Tuples.Select(t => $"({string.Join(", ", t.Select(t => t.Size))})")));

                sw.Flush();
            });
        }

        public static void TestBRKGA()
        {
            static float Variance(IEnumerable<float[]> g)
            {
                return Enumerable.Range(0, 19).Select(i => g.Select(a => a[i]).Variance()).Sum();
            }

            static ulong Decode(float[] keys, int size)
            {
                var indices = keys.Select((k, i) => (k, i)).OrderBy(t => t.k).Select(t => t.i).Take(size);

                ulong g = 0;
                foreach (var i in indices)
                    g |= 1UL << i;
                return g;
            }

            static float CalcExeCost(Individual<float[]> ind, int n_dsics)
            {
                return ind.Weights.NumOfEvaluation(n_dsics);
            }

            static (float, float) GetDepthFraction(Individual<float[]> ind1, Individual<float[]> ind2, int n_dsics)
            {
                float t1 = CalcExeCost(ind1, n_dsics);
                float t2 = CalcExeCost(ind2, n_dsics);

                int n1 = 12;
                int n2 = 54;

                if (Math.Abs(t1 - t2) < 1E-3)
                    return (0, 0);

                if (t1 > t2)
                {
                    float f = n1 * (t1 - t2) / (t2 * (n2 - n1));
                    return (1, 1 + f);
                }
                else
                {
                    float f = n1 * (t2 - t1) / (t1 * (n2 - n1));
                    return (1 + f, 1);
                }
            }

            var info = new GenomeInfo<float[]>()
            {
                NumStages = 1,
                NumTuples = 3,
                SizeMin = 8,
                SizeMax = 9,
                MaxNumWeights = (int)Math.Pow(3, 9),
                GenomeGenerator = () => Enumerable.Range(0, 19).Select(_ => (float)Random.NextDouble()).ToArray(),
                Decoder = Decode,
                VarianceT = Variance,
            };

            var ga = new GA<float[], Score<float[]>>()
            {
                Info = info,
                Evaluator = new PopulationEvaluatorRandomTournament<float[]>(new PopulationTrainerCoLearning(1, 54, 6400, true), 2, 54, 100 * 400)
                {
                    GetDepthFraction = GetDepthFraction
                },
                // Evaluator = new PopulationEvaluatorTrainingScore<float[]>(new PopulationTrainerCoLearning(1, 54, 3200, true)),
                Variator = new VariatorEliteArchive<float[]>()
                {
                    NumElites = 20,
                    NumCx = 60,
                    NumEliteMutants = 10,
                    NumRandomMutants = 10,
                    Crossover = new CrossoverEliteBiased(0.7F),
                    MutantElite = new MutantRK(0.08F, 0.08F),
                    Generator = info,
                },

                IO = new IndividualIO<float[]>()
                {
                    Info = info,
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
            using StreamWriter sw = File.AppendText(log);

            var log_v = $"ga/log_v_{DateTime.Now:yyyy_MM_dd_HH_mm}.csv";
            using StreamWriter sw_v = File.AppendText(log_v);

            var variances = new List<float>();

            // ga.Run(ga.IO.Load("ga/ind.dat"), (n_gen, time, pop) =>
            ga.Run(ga.Init(100), (n_gen, time, pop) =>
            {
                var score = pop.MinBy(ind => ind.score).First();

                foreach (var s in pop.OrderBy(s => s.score))
                {
                    sw.Write(s.score + ",");

                    foreach (var t in s.ind.Tuples)
                    {
                        sw.Write(",," + string.Join(", ", t.Select(t => t.TupleBit)));
                    }
                    sw.WriteLine();
                }

                foreach (var t in score.ind.Tuples)
                {
                    ulong[] tuples = t.Select(t => t.TupleBit).ToArray();

                    for (int i = 0; i < tuples.Length / 2.0F; i++)
                    {
                        var b1 = tuples[i * 2];

                        if (i * 2 + 1 < tuples.Length)
                        {
                            var b2 = tuples[i * 2 + 1];
                            Console.WriteLine(new Board(b1, Board.HorizontalMirror(b2)));
                        }
                        else
                        {
                            Console.WriteLine(new Board(b1, 0));
                        }
                    }
                    Console.WriteLine();
                }

                float v = info.Variance(pop.OrderBy(s => s.score).Select(s => s.ind).Take(10).ToArray());
                variances.Add(v);

                sw_v.WriteLine(variances.TakeLast(100).Average());

                Console.WriteLine($"Gen : {n_gen}, {time}");
                Console.WriteLine(variances.TakeLast(100).Average());
                Console.WriteLine(string.Join(", ", score.ind.Tuples.Select(t => $"({string.Join(", ", t.Select(t => t.Size))})")));

                sw.Flush();
                sw_v.Flush();
            });
        }
    }

    public class GA<T, U> where U : Score<T>
    {
        private static ThreadLocal<Random> ThreadLocalRandom { get; } = new ThreadLocal<Random>(() => new Random());

        public GenomeInfo<T> Info { get; set; }
        public IndividualIO<T> IO { get; set; }

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

        public void Run(List<Individual<T>> pop, Action<int, float, List<U>> logger)
        {
            var timer = new System.Diagnostics.Stopwatch();

            for (int i = 0; i < 10000; i++)
            {
                timer.Restart();

                Console.WriteLine("Evaluate");
                var scores = Evaluator.Evaluate(pop);

                timer.Stop();
                float time = (float)timer.ElapsedTicks / System.Diagnostics.Stopwatch.Frequency;
                logger(i, time, scores);

                pop = Variator.Vary(scores, Random);

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
        public T Genome { get; }
        public int Size { get; }
        public int NumWeights { get; }

        public GenomeGroup(T genome, int size)
        {
            Genome = genome;
            Size = size;
            NumWeights = (int)Math.Pow(3, Size);
        }
    }

    public class TupleData<T>
    {
        public T Genome { get; }
        public ulong TupleBit { get; }
        public GenomeInfo<T> Info { get; }

        public int Size { get; }

        public TupleData(T genome, int size, GenomeInfo<T> info)
        {
            Genome = genome;
            Size = size;
            Info = info;

            TupleBit = info.Decoder(genome, Size);
        }

        public override bool Equals(object obj)
        {
            if (obj is TupleData<T> t)
                return Equals(t);

            return false;
        }

        public bool Equals(TupleData<T> y)
        {
            if (ReferenceEquals(this, y))
                return true;

            return Size == y.Size && TupleBit == y.TupleBit;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Size, TupleBit);
        }
    }

    public class Individual<T>
    {
        public GenomeGroup<T>[][] Genome { get; }
        public TupleData<T>[][] Tuples { get; }

        public Weight Weights { get; }

        public GenomeInfo<T> Info { get; }

        public List<float> Log { get; } = new List<float>();

        public Individual(GenomeGroup<T>[][] genome, GenomeInfo<T> info)
        {
            Genome = genome;
            Info = info;

            Tuples = new TupleData<T>[info.NumStages][];
            var weights = new Weight[info.NumStages];

            for (int i = 0; i < genome.Length; i++)
            {
                var list = new List<TupleData<T>>();

                int n_weights = 0;
                foreach (var g in Genome[i])
                {
                    if (n_weights + g.NumWeights > info.MaxNumWeights)
                        continue;

                    n_weights += g.NumWeights;

                    list.Add(new TupleData<T>(g.Genome, g.Size, info));
                }
                Tuples[i] = list.OrderBy(t => t.TupleBit).ToArray();
                weights[i] = new WeightsSum(Tuples[i].Select(t => new WeightsArrayR(t.TupleBit)).ToArray());
            }

            Weights = new WeightsStagebased(weights);
        }

        public Evaluator CreateEvaluator() => new EvaluatorWeightsBased(Weights);

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

            if (Tuples.Length != y.Tuples.Length)
                return false;

            for (int i = 0; i < Tuples.Length; i++)
            {
                if (Tuples[i].Length != y.Tuples[i].Length)
                    return false;

                for (int j = 0; j < Tuples[i].Length; j++)
                    if (Tuples[i][j].TupleBit != y.Tuples[i][j].TupleBit)
                        return false;
            }
            return true;
        }

        public int GetHashCode(IEnumerable<TupleData<T>> tuples)
        {
            return tuples.Aggregate(0, (total, next) => HashCode.Combine(total, next.TupleBit));
        }

        public override int GetHashCode()
        {
            return Tuples.Aggregate(0, (total, next) => HashCode.Combine(total, GetHashCode(next)));
        }
    }
}
