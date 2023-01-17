using System;
using System.IO;
using System.Linq;

namespace OthelloAI
{
    public class EdaxUtil
    {
        const int A1 = 0, B1 = 1, C1 = 2, D1 = 3, E1 = 4, F1 = 5, G1 = 6, H1 = 7;
        const int A2 = 8 + 0, B2 = 8 + 1, C2 = 8 + 2, D2 = 8 + 3, E2 = 8 + 4, F2 = 8 + 5, G2 = 8 + 6, H2 = 8 + 7;
        const int A3 = 16 + 0, B3 = 16 + 1, C3 = 16 + 2, D3 = 16 + 3, E3 = 16 + 4, F3 = 16 + 5, G3 = 16 + 6, H3 = 16 + 7;
        const int A4 = 24 + 0, B4 = 24 + 1, C4 = 24 + 2, D4 = 24 + 3, E4 = 24 + 4, F4 = 24 + 5, G4 = 24 + 6, H4 = 24 + 7;
        const int A5 = 32 + 0, B5 = 32 + 1, C5 = 32 + 2, D5 = 32 + 3, E5 = 32 + 4, F5 = 32 + 5, G5 = 32 + 6, H5 = 32 + 7;
        const int A6 = 40 + 0, B6 = 40 + 1, C6 = 40 + 2, D6 = 40 + 3, E6 = 40 + 4, F6 = 40 + 5, G6 = 40 + 6, H6 = 40 + 7;
        const int A7 = 48 + 0, B7 = 48 + 1, C7 = 48 + 2, D7 = 48 + 3, E7 = 48 + 4, F7 = 48 + 5, G7 = 48 + 6, H7 = 48 + 7;
        const int A8 = 56 + 0, B8 = 56 + 1, C8 = 56 + 2, D8 = 56 + 3, E8 = 56 + 4, F8 = 56 + 5, G8 = 56 + 6, H8 = 56 + 7;
        const int PASS = 64, NOMOVE = 65;

        public static int[][] EVAL_F2X = {
            new [] {A1, B1, A2, B2, C1, A3, C2, B3, C3},
            new [] {H1, G1, H2, G2, F1, H3, F2, G3, F3},
            new [] {A8, A7, B8, B7, A6, C8, B6, C7, C6},
            new [] {H8, H7, G8, G7, H6, F8, G6, F7, F6},

            new [] {A5, A4, A3, A2, A1, B2, B1, C1, D1, E1},
            new [] {H5, H4, H3, H2, H1, G2, G1, F1, E1, D1},
            new [] {A4, A5, A6, A7, A8, B7, B8, C8, D8, E8},
            new [] {H4, H5, H6, H7, H8, G7, G8, F8, E8, D8},

            new [] {B2, A1, B1, C1, D1, E1, F1, G1, H1, G2},
            new [] {B7, A8, B8, C8, D8, E8, F8, G8, H8, G7},
            new [] {B2, A1, A2, A3, A4, A5, A6, A7, A8, B7},
            new [] {G2, H1, H2, H3, H4, H5, H6, H7, H8, G7},

            new [] {A1, C1, D1, C2, D2, E2, F2, E1, F1, H1},
            new [] {A8, C8, D8, C7, D7, E7, F7, E8, F8, H8},
            new [] {A1, A3, A4, B3, B4, B5, B6, A5, A6, A8},
            new [] {H1, H3, H4, G3, G4, G5, G6, H5, H6, H8},

            new [] {A2, B2, C2, D2, E2, F2, G2, H2},
            new [] {A7, B7, C7, D7, E7, F7, G7, H7},
            new [] {B1, B2, B3, B4, B5, B6, B7, B8},
            new [] {G1, G2, G3, G4, G5, G6, G7, G8},

            new [] {A3, B3, C3, D3, E3, F3, G3, H3},
            new [] {A6, B6, C6, D6, E6, F6, G6, H6},
            new [] {C1, C2, C3, C4, C5, C6, C7, C8},
            new [] {F1, F2, F3, F4, F5, F6, F7, F8},

            new [] {A4, B4, C4, D4, E4, F4, G4, H4},
            new [] {A5, B5, C5, D5, E5, F5, G5, H5},
            new [] {D1, D2, D3, D4, D5, D6, D7, D8},
            new [] {E1, E2, E3, E4, E5, E6, E7, E8},

            new [] {A1, B2, C3, D4, E5, F6, G7, H8},
            new [] {A8, B7, C6, D5, E4, F3, G2, H1},

            new [] {B1, C2, D3, E4, F5, G6, H7},
            new [] {H2, G3, F4, E5, D6, C7, B8},
            new [] {A2, B3, C4, D5, E6, F7, G8},
            new [] {G1, F2, E3, D4, C5, B6, A7},

            new [] {C1, D2, E3, F4, G5, H6},
            new [] {A3, B4, C5, D6, E7, F8},
            new [] {F1, E2, D3, C4, B5, A6},
            new [] {H3, G4, F5, E6, D7, C8},

            new [] {D1, E2, F3, G4, H5},
            new [] {A4, B5, C6, D7, E8},
            new [] {E1, D2, C3, B4, A5},
            new [] {H4, G5, F6, E7, D8},

            new [] {D1, C2, B3, A4},
            new [] {A5, B6, C7, D8},
            new [] {E1, F2, G3, H4},
            new [] {H5, G6, F7, E8},

            new [] {NOMOVE}
        };

        public static int[] EVAL_OFFSET = {
         0,      0,      0,      0,
     19683,  19683,  19683,  19683,
     78732,  78732,  78732,  78732,
    137781, 137781, 137781, 137781,
    196830, 196830, 196830, 196830,
    203391, 203391, 203391, 203391,
    209952, 209952, 209952, 209952,
    216513, 216513,
    223074, 223074, 223074, 223074,
    225261, 225261, 225261, 225261,
    225990, 225990, 225990, 225990,
    226233, 226233, 226233, 226233,
    226314,
};

        static int[][] EVAL_C10 = { new int[59049], new int[59049] };
        static int[][] EVAL_S10 = { new int[59049], new int[59049] };
        static int[][] EVAL_C9 = { new int[19683], new int[19683] };
        static int[][] EVAL_S8 = { new int[6561], new int[6561] };
        static int[][] EVAL_S7 = { new int[2187], new int[2187] };
        static int[][] EVAL_S6 = { new int[729], new int[729] };
        static int[][] EVAL_S5 = { new int[243], new int[243] };
        static int[][] EVAL_S4 = { new int[81], new int[81] };

        readonly static int[] EVAL_PACKED_SIZE = { 10206, 29889, 29646, 29646, 3321, 3321, 3321, 3321, 1134, 378, 135, 45, 1 };
        readonly static int[] EVAL_SIZE = { 19683, 59049, 59049, 59049, 6561, 6561, 6561, 6561, 2187, 729, 243, 81, 1 };

        /** number of (unpacked) weights */
        const int EVAL_N_WEIGHT = 226315;
        /** number of plies */
        const int EVAL_N_PLY = 61;
        /** number of features */
        const int EVAL_N_FEATURE = 47;

        static int[] o = { 1, 0, 2 };

        static int opponent_feature(int l, int d)
        {
            int f = o[l % 3];
            if (d > 1) f += opponent_feature(l / 3, d - 1) * 3;
            return f;
        }

        public static short[][][] Open(BinaryReader reader)
        {
            int[] T = new int[59049];
            int i, j, k, l, n;
            int offset;

            for (l = n = 0; l < 6561; l++)
            { /* 8 squares : 6561 -> 3321 */
                k = ((l / 2187) % 3) + ((l / 729) % 3) * 3 + ((l / 243) % 3) * 9 +
                ((l / 81) % 3) * 27 + ((l / 27) % 3) * 81 + ((l / 9) % 3) * 243 +
                ((l / 3) % 3) * 729 + (l % 3) * 2187;
                if (k < l) T[l] = T[k];
                else T[l] = n++;
                EVAL_S8[0][l] = T[l];
                EVAL_S8[1][opponent_feature(l, 8)] = T[l];
            }
            for (l = n = 0; l < 2187; l++)
            { /* 7 squares : 2187 -> 1134 */
                k = ((l / 729) % 3) + ((l / 243) % 3) * 3 + ((l / 81) % 3) * 9 +
                 ((l / 27) % 3) * 27 + ((l / 9) % 3) * 81 + ((l / 3) % 3) * 243 +
                 (l % 3) * 729;
                if (k < l) T[l] = T[k];
                else T[l] = n++;
                EVAL_S7[0][l] = T[l];
                EVAL_S7[1][opponent_feature(l, 7)] = T[l];
            }
            for (l = n = 0; l < 729; l++)
            { /* 6 squares : 729 -> 378 */
                k = ((l / 243) % 3) + ((l / 81) % 3) * 3 + ((l / 27) % 3) * 9 +
                 ((l / 9) % 3) * 27 + ((l / 3) % 3) * 81 + (l % 3) * 243;
                if (k < l) T[l] = T[k];
                else T[l] = n++;
                EVAL_S6[0][l] = T[l];
                EVAL_S6[1][opponent_feature(l, 6)] = T[l];
            }
            for (l = n = 0; l < 243; l++)
            { /* 5 squares : 243 -> 135 */
                k = ((l / 81) % 3) + ((l / 27) % 3) * 3 + ((l / 9) % 3) * 9 +
                ((l / 3) % 3) * 27 + (l % 3) * 81;
                if (k < l) T[l] = T[k];
                else T[l] = n++;
                EVAL_S5[0][l] = T[l];
                EVAL_S5[1][opponent_feature(l, 5)] = T[l];
            }
            for (l = n = 0; l < 81; l++)
            { /* 4 squares : 81 -> 45 */
                k = ((l / 27) % 3) + ((l / 9) % 3) * 3 + ((l / 3) % 3) * 9 + (l % 3) * 27;
                if (k < l) T[l] = T[k];
                else T[l] = n++;
                EVAL_S4[0][l] = T[l];
                EVAL_S4[1][opponent_feature(l, 4)] = T[l];
            }
            for (l = n = 0; l < 19683; l++)
            { /* 9 corner squares : 19683 -> 10206 */
                k = ((l / 6561) % 3) * 6561 + ((l / 729) % 3) * 2187 +
                ((l / 2187) % 3) * 729 + ((l / 243) % 3) * 243 + ((l / 27) % 3) * 81 +
                ((l / 81) % 3) * 27 + ((l / 3) % 3) * 9 + ((l / 9) % 3) * 3 + (l % 3);
                if (k < l) T[l] = T[k];
                else T[l] = n++;
                EVAL_C9[0][l] = T[l];
                EVAL_C9[1][opponent_feature(l, 9)] = T[l];
            }
            for (l = n = 0; l < 59049; l++)
            { /* 10 squares (edge +X ) : 59049 -> 29646 */
                k = ((l / 19683) % 3) + ((l / 6561) % 3) * 3 + ((l / 2187) % 3) * 9 +
                  ((l / 729) % 3) * 27 + ((l / 243) % 3) * 81 + ((l / 81) % 3) * 243 +
                  ((l / 27) % 3) * 729 + ((l / 9) % 3) * 2187 + ((l / 3) % 3) * 6561 +
                  (l % 3) * 19683;
                if (k < l) T[l] = T[k];
                else T[l] = n++;
                EVAL_S10[0][l] = T[l];
                EVAL_S10[1][opponent_feature(l, 10)] = T[l];
            }
            for (l = n = 0; l < 59049; l++)
            { /* 10 squares (angle + X) : 59049 -> 29889 */
                k = ((l / 19683) % 3) + ((l / 6561) % 3) * 3 + ((l / 2187) % 3) * 9 +
                  ((l / 729) % 3) * 27 + ((l / 243) % 3) * 243 + ((l / 81) % 3) * 81 +
                  ((l / 27) % 3) * 729 + ((l / 9) % 3) * 2187 + ((l / 3) % 3) * 6561 +
                  (l % 3) * 19683;
                if (k < l) T[l] = T[k];
                else T[l] = n++;
                EVAL_C10[0][l] = T[l];
                EVAL_C10[1][opponent_feature(l, 10)] = T[l];
            }

            int n_w = 114364;
            short[][][] EVAL_WEIGHT = 2.Loop(_ => EVAL_N_PLY.Loop(_ => new short[EVAL_N_WEIGHT]).ToArray()).ToArray();

            byte[] header = reader.ReadBytes(28);
            // Console.WriteLine(string.Join(", ", header));

            for (int ply = 0; ply < EVAL_N_PLY; ply++)
            {
                short[] w = n_w.Loop(_ => reader.ReadInt16()).ToArray();

                i = j = offset = 0;
                for (k = 0; k < EVAL_SIZE[i]; k++, j++)
                {
                    EVAL_WEIGHT[0][ply][j] = w[EVAL_C9[0][k] + offset];
                    EVAL_WEIGHT[1][ply][j] = w[EVAL_C9[1][k] + offset];
                }
                offset += EVAL_PACKED_SIZE[i];
                i++;
                for (k = 0; k < EVAL_SIZE[i]; k++, j++)
                {
                    EVAL_WEIGHT[0][ply][j] = w[EVAL_C10[0][k] + offset];
                    EVAL_WEIGHT[1][ply][j] = w[EVAL_C10[1][k] + offset];
                }
                offset += EVAL_PACKED_SIZE[i];
                i++;
                for (k = 0; k < EVAL_SIZE[i]; k++, j++)
                {
                    EVAL_WEIGHT[0][ply][j] = w[EVAL_S10[0][k] + offset];
                    EVAL_WEIGHT[1][ply][j] = w[EVAL_S10[1][k] + offset];
                }
                offset += EVAL_PACKED_SIZE[i];
                i++;
                for (k = 0; k < EVAL_SIZE[i]; k++, j++)
                {
                    EVAL_WEIGHT[0][ply][j] = w[EVAL_S10[0][k] + offset];
                    EVAL_WEIGHT[1][ply][j] = w[EVAL_S10[1][k] + offset];
                }
                offset += EVAL_PACKED_SIZE[i];
                i++;
                for (k = 0; k < EVAL_SIZE[i]; k++, j++)
                {
                    EVAL_WEIGHT[0][ply][j] = w[EVAL_S8[0][k] + offset];
                    EVAL_WEIGHT[1][ply][j] = w[EVAL_S8[1][k] + offset];
                }
                offset += EVAL_PACKED_SIZE[i];
                i++;
                for (k = 0; k < EVAL_SIZE[i]; k++, j++)
                {
                    EVAL_WEIGHT[0][ply][j] = w[EVAL_S8[0][k] + offset];
                    EVAL_WEIGHT[1][ply][j] = w[EVAL_S8[1][k] + offset];
                }
                offset += EVAL_PACKED_SIZE[i];
                i++;
                for (k = 0; k < EVAL_SIZE[i]; k++, j++)
                {
                    EVAL_WEIGHT[0][ply][j] = w[EVAL_S8[0][k] + offset];
                    EVAL_WEIGHT[1][ply][j] = w[EVAL_S8[1][k] + offset];
                }
                offset += EVAL_PACKED_SIZE[i];
                i++;
                for (k = 0; k < EVAL_SIZE[i]; k++, j++)
                {
                    EVAL_WEIGHT[0][ply][j] = w[EVAL_S8[0][k] + offset];
                    EVAL_WEIGHT[1][ply][j] = w[EVAL_S8[1][k] + offset];
                }
                offset += EVAL_PACKED_SIZE[i];
                i++;
                for (k = 0; k < EVAL_SIZE[i]; k++, j++)
                {
                    EVAL_WEIGHT[0][ply][j] = w[EVAL_S7[0][k] + offset];
                    EVAL_WEIGHT[1][ply][j] = w[EVAL_S7[1][k] + offset];
                }
                offset += EVAL_PACKED_SIZE[i];
                i++;
                for (k = 0; k < EVAL_SIZE[i]; k++, j++)
                {
                    EVAL_WEIGHT[0][ply][j] = w[EVAL_S6[0][k] + offset];
                    EVAL_WEIGHT[1][ply][j] = w[EVAL_S6[1][k] + offset];
                }
                offset += EVAL_PACKED_SIZE[i];
                i++;
                for (k = 0; k < EVAL_SIZE[i]; k++, j++)
                {
                    EVAL_WEIGHT[0][ply][j] = w[EVAL_S5[0][k] + offset];
                    EVAL_WEIGHT[1][ply][j] = w[EVAL_S5[1][k] + offset];
                }
                offset += EVAL_PACKED_SIZE[i];
                i++;
                for (k = 0; k < EVAL_SIZE[i]; k++, j++)
                {
                    EVAL_WEIGHT[0][ply][j] = w[EVAL_S4[0][k] + offset];
                    EVAL_WEIGHT[1][ply][j] = w[EVAL_S4[1][k] + offset];
                }
                offset += EVAL_PACKED_SIZE[i];
                i++;
                EVAL_WEIGHT[0][ply][j] = w[offset];
                EVAL_WEIGHT[1][ply][j] = w[offset];
            }

            return EVAL_WEIGHT;
        }

        static int board_get_square_color(Board board, int x)
        {
            return 2 - 2 * (int)((board.bitB >> x) & 1) - (int)((board.bitW >> x) & 1);
        }

        public static int[] CreateFeature(Board board)
        {
            int i, j, c;
            int[] feature = new int[EVAL_N_FEATURE];

            for (i = 0; i < EVAL_N_FEATURE; ++i)
            {
                feature[i] = 0;
                for (j = 0; j < EVAL_F2X[i].Length; j++)
                {
                    c = board_get_square_color(board, EVAL_F2X[i][j]);
                    feature[i] = feature[i] * 3 + c;
                }
                feature[i] += EVAL_OFFSET[i];
            }
            return feature;
        }

        public static ulong ConvertToMask(int id)
        {
            int[] f2x = EVAL_F2X[id];

            ulong dst = 0;
            for (int i = 0; i < f2x.Length; i++)
            {
                dst |= 1UL << f2x[i];
            }
            return dst;
        }

        public static float[] ConvertToMyWeight(short[] w, int id)
        {
            int[] f2x = EVAL_F2X[id];
            int offset = EVAL_OFFSET[id];

            int len = (int)Math.Pow(3, f2x.Length);

            float[] dst = new float[len];

            int[] indices = f2x.OrderBy(x => x).Select(x => Array.IndexOf(f2x, x)).ToArray();
            int[] color = { 1, 2, 0 };

            for (int i = 0; i < len; i++)
            {
                int[] a = WeightUtil.Disassemble(i, f2x.Length);
                a = a.Reverse().ToArray();
                a = indices.Select(x => a[x]).Select(x => color[x]).ToArray();
                int hash = WeightUtil.Assemble(a);

                dst[hash] = w[i + offset] / 128F;
            }

            return dst;
        }

        public static float[] ConvertToMyWeight2(short[] w, int id)
        {
            int[] f2x = EVAL_F2X[id];
            int offset = EVAL_OFFSET[id];

            int len = (int)Math.Pow(3, f2x.Length);

            float[] dst = new float[len];

            for (int i = 0; i < len; i++)
            {
                dst[i] = w[i + offset] / 128F;
            }

            return dst;
        }
    }

    public class WeightEdax : Weight
    {
        short[][][] weight;

        const int SCORE_MIN = -64;
        const int SCORE_MAX = 64;

        public override void ApplyTrainedEvaluation(float range)
        {
            throw new System.NotImplementedException();
        }

        public override Weight Copy()
        {
            throw new System.NotImplementedException();
        }

        public int Eval(Board b)
        {
            short[] w = weight[b.n_stone & 1][b.n_stone - 4];
            int[] f = EdaxUtil.CreateFeature(b);

            int score = w[f[0]] + w[f[1]] + w[f[2]] + w[f[3]]
                            + w[f[4]] + w[f[5]] + w[f[6]] + w[f[7]]
                            + w[f[8]] + w[f[9]] + w[f[10]] + w[f[11]]
                            + w[f[12]] + w[f[13]] + w[f[14]] + w[f[15]]
                            + w[f[16]] + w[f[17]] + w[f[18]] + w[f[19]]
                            + w[f[20]] + w[f[21]] + w[f[22]] + w[f[23]]
                            + w[f[24]] + w[f[25]]
                            + w[f[26]] + w[f[27]] + w[f[28]] + w[f[29]]
                            + w[f[30]] + w[f[31]] + w[f[32]] + w[f[33]]
                            + w[f[34]] + w[f[35]] + w[f[36]] + w[f[37]]
                            + w[f[38]] + w[f[39]] + w[f[40]] + w[f[41]]
                            + w[f[42]] + w[f[43]] + w[f[44]] + w[f[45]]
                            + w[226314];

            if (score > 0) score += 64; else score -= 64;
            score /= 128;

            if (score <= SCORE_MIN) score = SCORE_MIN + 1;
            else if (score >= SCORE_MAX) score = SCORE_MAX - 1;

            return score * -((b.n_stone & 1) * 2 - 1);
        }

        public override int Eval(RotatedAndMirroredBoards b)
        {
            return Eval(b.rot0);
        }

        public override float EvalTraining(RotatedAndMirroredBoards b)
        {
            return Eval(b.rot0);
        }

        public override float[] GetWeights()
        {
            throw new System.NotImplementedException();
        }

        public override int NumOfEvaluation(int n_discs) => 47;

        public override void Read(BinaryReader reader)
        {
            weight = EdaxUtil.Open(reader);
        }

        public override void Reset()
        {
            throw new System.NotImplementedException();
        }

        public override void UpdataEvaluation(Board board, float add, float range)
        {
            throw new System.NotImplementedException();
        }

        public override void Write(BinaryWriter writer)
        {
            throw new System.NotImplementedException();
        }

        public WeightArray[] Convert1()
        {
            short[] w = weight[0][36];

            return (new[] { 0, 4, 8, 12, 16, 20, 24, 28, 30, 34, 38, 42 }).Select(i =>
            {
                float[] w2 = EdaxUtil.ConvertToMyWeight(w, i);
                ulong mask = EdaxUtil.ConvertToMask(i);

                if (i == 28)
                {
                    for (int j = 0; j < w2.Length; j++)
                        w2[j] = w2[j] * 0.25F + w[^1] / 128F * 0.125F;
                }
                else
                {
                    for (int j = 0; j < w2.Length; j++)
                        w2[j] = w2[j] * 0.5F;
                }

                return new WeightArrayPextHashingTer(mask) { weights = w2 };
            }).ToArray();
        }

        public WeightArray[] Convert2()
        {
            short[] w = weight[0][36];

            return (new[] { 0, 4, 8, 12, 16, 20, 24, 28, 30, 34, 38, 42 }).Select(i =>
            {
                float[] w2 = EdaxUtil.ConvertToMyWeight2(w, i);
                ulong mask = EdaxUtil.ConvertToMask(i);

                Console.WriteLine(string.Join(", ", EdaxUtil.EVAL_F2X[i]));
                Console.WriteLine(new Board(mask, 0));

                if (i == 28)
                {
                    for (int j = 0; j < w2.Length; j++)
                        w2[j] = w2[j] * 0.25F + w[^1] / 128F * 0.125F;
                }
                else
                {
                    for (int j = 0; j < w2.Length; j++)
                        w2[j] = w2[j] * 0.5F;
                }

                return new WeightArrayScanning(EdaxUtil.EVAL_F2X[i]) { weights = w2 };
            }).ToArray();
        }

        public static void Test()
        {
            var weight = new WeightEdax();
            weight.Load("eval.dat");

            var w = new WeightsSum(weight.Convert1());

            static bool Within(TrainingDataElement d) => 40 <= d.board.n_stone && d.board.n_stone <= 40;
            var data = Enumerable.Range(2001, 15).SelectMany(i => WthorRecordReader.Read($"WTH/WTH_{i}.wtb").SelectMany(x => x).Where(Within).ToArray()).ToArray();

            //foreach(var d in data)
            //{
            //    Console.WriteLine(d.board);
            //    Console.WriteLine(w.EvalTraining(new RotatedAndMirroredBoards(d.board)));
            //}

            float eval(Weight x, Board b)
            {
                float s = x.EvalTraining(new RotatedAndMirroredBoards(b));
                if (s > 0)
                    s += 0.5F;
                else
                    s -= 0.5F;
                return Math.Clamp(s, -64, 64);
            }

            float e = data.Select(d => d.result - eval(w, d.board)).Select(x => x * x).Average();
            // float e = data.Select(d => d.result - weight.Eval(d.board)).Select(x => x * x).Average();
            Console.WriteLine(e);
        }
    }
}
