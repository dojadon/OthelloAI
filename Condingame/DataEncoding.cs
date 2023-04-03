using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace OthelloAI.Condingame
{
    public class DataEncoding
    {
        readonly static int[] PS = { 10206, 29889, 29646, 29646, 3321, 3321, 3321, 3321, 1134, 378, 135, 45, 1 };
        readonly static int[] S = { 19683, 59049, 59049, 59049, 6561, 6561, 6561, 6561, 2187, 729, 243, 81, 1 };

        public const int EVAL_N_PACKED_WEIGHT = 114364;
        /** number of (unpacked) weights */
        public const int EVAL_N_WEIGHT = 226315;
        /** number of plies */
        public const int EVAL_N_PLY = 2;
        /** number of features */
        public const int EVAL_N_FEATURE = 47;

        public static double[][] AverageStage(short[][] w)
        {
            List<short[]>[] w_grouped = EVAL_N_PLY.Loop(_ => new List<short[]>()).ToArray();

            for (int i = 4; i < 50; i++)
            {
                int stage = EdaxWeightLight.GetStage(i);
                w_grouped[stage].Add(w[i]);
            }
            return w_grouped.Select(ls => w[0].Length.Loop(i => ls.Select(a => (int)a[i]).Average()).ToArray()).ToArray();
        }

        public static void EncodeWeight()
        {
            using var reader = new BinaryReader(new FileStream("eval.dat", FileMode.Open));
            short[][] packed_w_src = EdaxUtil.OpenPackedWeight(reader);
            short[][][] w_src = EdaxUtil.Open(packed_w_src);

            double[][] avg_w = AverageStage(w_src[0]);
            double[][] avg_packed_w = AverageStage(packed_w_src);

            double Percentile(double[] a, double p)
            {
                int index = Math.Clamp((int)(a.Length * p), 0, a.Length - 1);
                return a[index];
            }

            byte To3bit(double d, double[] borders)
            {
                for (byte i = 0; i < 7; i++)
                    if (d < borders[i])
                        return i;
                return 7;
            }

            byte[][] weight = new byte[EVAL_N_PLY][];
            short[] weight_const = new short[EVAL_N_PLY];
            short[][][] percentiles = new short[EVAL_N_PLY][][];

            for (int ply = 0; ply < EVAL_N_PLY; ply++)
            {
                weight[ply] = new byte[EVAL_N_PACKED_WEIGHT];
                percentiles[ply] = new short[S.Length][];

                int offset = 0;

                for (int i = 0; i < S.Length - 1; i++)
                {
                    double[] t_w = new double[S[i]];
                    Array.Copy(avg_w[ply], offset, t_w, 0, t_w.Length);
                    Array.Sort(t_w);

                    double[] border = 7.Loop(p => Percentile(t_w, (p + 1) / 8.0)).ToArray();

                    percentiles[ply][i] = 8.Loop(p => (short)Percentile(t_w, (p * 2 + 1) / 16.0)).ToArray();
                    Console.WriteLine($"new short[] {{ {string.Join(", ", percentiles[ply][i])} }},");

                    for (int j = 0; j < PS[i]; j++)
                        weight[ply][offset + j] = To3bit(avg_packed_w[ply][offset + j], border);

                    offset += PS[i];
                }

                for (int i = 0; i < 8; i++)
                {
                    Console.WriteLine($"Count : {i}, {weight[ply].Count(j => j == i)}");
                }
                weight_const[ply] = (short)avg_w[ply][^1];
                Console.WriteLine($"Const: {weight_const[ply]}");
            }

            byte[] flatten = weight.SelectMany(a => a).ToArray();

            string s = DataEncoding.Encode3bitData(flatten);
            File.WriteAllText("codingame/encoded.txt", s);
        }

        public static void Test()
        {
            var rand = new Random();
            byte[] data = new byte[15 * 1000];
            rand.NextBytes(data);

            string s = Encode(data);
            byte[] test = Decode(s);

            Console.WriteLine($"{data.Length}, {test.Length}");

            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] != test[i])
                    Console.WriteLine($"Failure! : {i}");
            }
        }

        public static string Encode3bitData(byte[] data)
        {
            byte[] encoded_data = new byte[data.Length / 5 * 3 + 3];

            for (int i = 0; i < data.Length / 5 + 1; i++)
            {
                int p = 0;
                for(int j = 0; j < 5; j++)
                {
                    if (i * 5 + j >= data.Length)
                        break;

                    p |= data[i * 5 + j] << (j * 3);
                }
                Encode(p, encoded_data, i * 3);
            }

            return Encoding.UTF8.GetString(encoded_data);
        }

        public static byte[] Decode3bitData(string encoded)
        {
            byte[] data = Encoding.UTF8.GetBytes(encoded);

            byte[] decoded = new byte[data.Length / 3 * 5];

            for (int i = 0; i < data.Length / 3; i++)
            {
                int p = Decode(data, i * 3);

                for(int j = 0; j < 5; j++)
                {
                    decoded[i * 5 + j] = (byte)((p >> (j * 3)) & 0b111);
                }
            }

            return decoded;
        }

        public static string Encode(byte[] data)
        {
            byte[] encoded_data = new byte[data.Length / 15 * 8 * 3];

            byte[] test = new byte[data.Length];

            for (int i = 0; i < data.Length / 15; i++)
            {
                int[] p = EncodeBlock(data, i * 15);
                DecodeBlock(p, test, i * 15);

                for (int j = 0; j < 15; j++)
                {
                    if (data[i * 15 + j] != test[i * 15 + j])
                    {
                        Console.WriteLine($"failure encoding: {i}, {j}");
                    }
                }

                for (int j = 0; j < p.Length; j++)
                {
                    Encode(p[j], encoded_data, (i * 8 + j) * 3);
                }
            }

            return Encoding.UTF8.GetString(encoded_data);
        }

        public static int[] EncodeBlock(byte[] data, int o)
        {
            int[] p = new int[8];

            p[0] = ((data[o + 1] & 0b01111111) << 8) | data[o];
            p[1] = ((data[o + 3] & 0b111111) << 9) | (data[o + 2] << 1) | (data[o + 1] >> 7);
            p[2] = ((data[o + 5] & 0b11111) << 10) | (data[o + 4] << 2) | (data[o + 3] >> 6);
            p[3] = ((data[o + 7] & 0b1111) << 11) | (data[o + 6] << 3) | (data[o + 5] >> 5);
            p[4] = ((data[o + 9] & 0b111) << 12) | (data[o + 8] << 4) | (data[o + 7] >> 4);
            p[5] = ((data[o + 11] & 0b11) << 13) | (data[o + 10] << 5) | (data[o + 9] >> 3);
            p[6] = ((data[o + 13] & 0b1) << 14) | (data[o + 12] << 6) | (data[o + 11] >> 2);
            p[7] = (data[o + 14] << 7) | (data[o + 13] >> 1);

            return p;
        }

        public static void Encode(int i, byte[] dst, int offset)
        {
            if ((i & 0xFFFF8000) != 0)
                Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");

            i += 0x4000;

            if (i >= 0xA000)
                i += 0x1000;

            dst[offset + 0] = (byte)(0xE0 | (i >> 12));
            dst[offset + 1] = (byte)(0x80 | ((i >> 6) & 0b111111));
            dst[offset + 2] = (byte)(0x80 | (i & 0b111111));
        }

        public static byte[] Decode(string encoded)
        {
            byte[] data = Encoding.UTF8.GetBytes(encoded);

            byte[] decoded = new byte[data.Length / 3 / 8 * 15];

            for (int i = 0; i < data.Length / 8 / 3; i++)
            {
                int[] p = new int[8];

                for (int j = 0; j < 8; j++)
                {
                    p[j] = Decode(data, i * 24 + j * 3);
                }
                DecodeBlock(p, decoded, i * 15);
            }

            return decoded;
        }

        public static void DecodeBlock(int[] p, byte[] dst, int o)
        {
            static int Mask(int i)
            {
                return (1 << i) - 1;
            }

            for (int i = 0; i < 7; i++)
            {
                dst[o + i * 2] = (byte)((p[i] >> i) & 0xFF);

                int b1 = (p[i + 1] & Mask(i + 1)) << (7 - i);
                int b2 = (p[i] >> (8 + i)) & Mask(7 - i);
                dst[o + i * 2 + 1] = (byte)(b1 | b2);
            }

            dst[o + 14] = (byte)((p[7] >> 7) & 0xFF);

            //dst[0] = (byte)(p[0] & 0xFF);
            //dst[1] = (byte)(((p[1] & 1) << 7) | ((p[0] >> 8) & 0b1111111));

            //dst[2] = (byte)((p[1] >> 1) & 0xFF);
            //dst[3] = (byte)(((p[2] & 0b11) << 6) | ((p[1] >> 9) & 0b111111));

            //dst[4] = (byte)((p[2] >> 2) & 0xFF);
            //dst[5] = (byte)(((p[2] & 0b111) << 5) | ((p[1] >> 10) & 0b11111));

            //dst[6] = (byte)((p[2] >> 2) & 0xFF);
            //dst[7] = (byte)(((p[2] & 0b111) << 5) | ((p[1] >> 10) & 0b11111));
        }

        public static int Decode(byte[] src, int offset)
        {
            byte b1 = src[offset];
            byte b2 = src[offset + 1];
            byte b3 = src[offset + 2];

            int i = ((b1 & 0xF) << 12) | ((b2 & 0b111111) << 6) | (b3 & 0b111111);

            if (i >= 0xB000)
                i -= 0x1000;

            i -= 0x4000;

            return i;
        }
    }
}
