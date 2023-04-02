﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OthelloAI.Condingame
{
    public class EdaxWeightLight
    {
        const int A1 = 0, B1 = 1, C1 = 2, D1 = 3, E1 = 4, F1 = 5, G1 = 6, H1 = 7;
        const int A2 = 8, B2 = 9, C2 = 10, D2 = 11, E2 = 12, F2 = 13, G2 = 14, H2 = 15;
        const int A3 = 16, B3 = 17, C3 = 18, D3 = 19, E3 = 20, F3 = 21, G3 = 22, H3 = 23;
        const int A4 = 24, B4 = 25, C4 = 26, D4 = 27, E4 = 28, F4 = 29, G4 = 30, H4 = 31;
        const int A5 = 32, B5 = 33, C5 = 34, D5 = 35, E5 = 36, F5 = 37, G5 = 38, H5 = 39;
        const int A6 = 40, B6 = 41, C6 = 42, D6 = 43, E6 = 44, F6 = 45, G6 = 46, H6 = 47;
        const int A7 = 48, B7 = 49, C7 = 50, D7 = 51, E7 = 52, F7 = 53, G7 = 54, H7 = 55;
        const int A8 = 56, B8 = 57, C8 = 58, D8 = 59, E8 = 60, F8 = 61, G8 = 62, H8 = 63;
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

        static int[] EVAL_C10 =  new int[59049];
        static int[] EVAL_S10 =  new int[59049];
        static int[] EVAL_C9 = new int[19683];
        static int[] EVAL_S8 =  new int[6561];
        static int[] EVAL_S7 =  new int[2187];
        static int[] EVAL_S6 =  new int[729];
        static int[] EVAL_S5 =  new int[243];
        static int[] EVAL_S4 =  new int[81] ;

        readonly static int[] PS = { 10206, 29889, 29646, 29646, 3321, 3321, 3321, 3321, 1134, 378, 135, 45, 1 };
        readonly static int[] S = { 19683, 59049, 59049, 59049, 6561, 6561, 6561, 6561, 2187, 729, 243, 81, 1 };

        /** number of (unpacked) weights */
        const int EVAL_N_WEIGHT = 226315;
        /** number of plies */
        const int EVAL_N_PLY = 3;
        /** number of features */
        const int EVAL_N_FEATURE = 47;

        static int[] o = { 1, 0, 2 };

        static byte[][] weight;

        public static byte[][] Open(byte[] w)
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
                EVAL_S8[l] = T[l];
            }
            for (l = n = 0; l < 2187; l++)
            { /* 7 squares : 2187 -> 1134 */
                k = ((l / 729) % 3) + ((l / 243) % 3) * 3 + ((l / 81) % 3) * 9 +
                 ((l / 27) % 3) * 27 + ((l / 9) % 3) * 81 + ((l / 3) % 3) * 243 +
                 (l % 3) * 729;
                if (k < l) T[l] = T[k];
                else T[l] = n++;
                EVAL_S7[l] = T[l];
            }
            for (l = n = 0; l < 729; l++)
            { /* 6 squares : 729 -> 378 */
                k = ((l / 243) % 3) + ((l / 81) % 3) * 3 + ((l / 27) % 3) * 9 +
                 ((l / 9) % 3) * 27 + ((l / 3) % 3) * 81 + (l % 3) * 243;
                if (k < l) T[l] = T[k];
                else T[l] = n++;
                EVAL_S6[l] = T[l];
            }
            for (l = n = 0; l < 243; l++)
            { /* 5 squares : 243 -> 135 */
                k = ((l / 81) % 3) + ((l / 27) % 3) * 3 + ((l / 9) % 3) * 9 +
                ((l / 3) % 3) * 27 + (l % 3) * 81;
                if (k < l) T[l] = T[k];
                else T[l] = n++;
                EVAL_S5[l] = T[l];
            }
            for (l = n = 0; l < 81; l++)
            { /* 4 squares : 81 -> 45 */
                k = ((l / 27) % 3) + ((l / 9) % 3) * 3 + ((l / 3) % 3) * 9 + (l % 3) * 27;
                if (k < l) T[l] = T[k];
                else T[l] = n++;
                EVAL_S4[l] = T[l];
            }
            for (l = n = 0; l < 19683; l++)
            { /* 9 corner squares : 19683 -> 10206 */
                k = ((l / 6561) % 3) * 6561 + ((l / 729) % 3) * 2187 +
                ((l / 2187) % 3) * 729 + ((l / 243) % 3) * 243 + ((l / 27) % 3) * 81 +
                ((l / 81) % 3) * 27 + ((l / 3) % 3) * 9 + ((l / 9) % 3) * 3 + (l % 3);
                if (k < l) T[l] = T[k];
                else T[l] = n++;
                EVAL_C9[l] = T[l];
            }
            for (l = n = 0; l < 59049; l++)
            { /* 10 squares (edge +X ) : 59049 -> 29646 */
                k = ((l / 19683) % 3) + ((l / 6561) % 3) * 3 + ((l / 2187) % 3) * 9 +
                  ((l / 729) % 3) * 27 + ((l / 243) % 3) * 81 + ((l / 81) % 3) * 243 +
                  ((l / 27) % 3) * 729 + ((l / 9) % 3) * 2187 + ((l / 3) % 3) * 6561 +
                  (l % 3) * 19683;
                if (k < l) T[l] = T[k];
                else T[l] = n++;
                EVAL_S10[l] = T[l];
            }
            for (l = n = 0; l < 59049; l++)
            { /* 10 squares (angle + X) : 59049 -> 29889 */
                k = ((l / 19683) % 3) + ((l / 6561) % 3) * 3 + ((l / 2187) % 3) * 9 +
                  ((l / 729) % 3) * 27 + ((l / 243) % 3) * 243 + ((l / 81) % 3) * 81 +
                  ((l / 27) % 3) * 729 + ((l / 9) % 3) * 2187 + ((l / 3) % 3) * 6561 +
                  (l % 3) * 19683;
                if (k < l) T[l] = T[k];
                else T[l] = n++;
                EVAL_C10[l] = T[l];
            }

            weight = EVAL_N_PLY.Loop(_ => new byte[EVAL_N_WEIGHT]).ToArray();

            for (int ply = 0; ply < EVAL_N_PLY; ply++)
            {
                offset = 114364 * ply;
                i = j = 0;
                for (k = 0; k < S[i]; k++, j++)
                {
                    weight[ply][j] = w[EVAL_C9[k] + offset];
                }
                offset += PS[i];
                i++;
                for (k = 0; k < S[i]; k++, j++)
                {
                    weight[ply][j] = w[EVAL_C10[k] + offset];
                }
                offset += PS[i];
                i++;
                for (k = 0; k < S[i]; k++, j++)
                {
                    weight[ply][j] = w[EVAL_S10[k] + offset];
                }
                offset += PS[i];
                i++;
                for (k = 0; k < S[i]; k++, j++)
                {
                    weight[ply][j] = w[EVAL_S10[k] + offset];
                }
                offset += PS[i];
                i++;
                for (k = 0; k < S[i]; k++, j++)
                {
                    weight[ply][j] = w[EVAL_S8[k] + offset];
                }
                offset += PS[i];
                i++;
                for (k = 0; k < S[i]; k++, j++)
                {
                    weight[ply][j] = w[EVAL_S8[k] + offset];
                }
                offset += PS[i];
                i++;
                for (k = 0; k < S[i]; k++, j++)
                {
                    weight[ply][j] = w[EVAL_S8[k] + offset];
                }
                offset += PS[i];
                i++;
                for (k = 0; k < S[i]; k++, j++)
                {
                    weight[ply][j] = w[EVAL_S8[k] + offset];
                }
                offset += PS[i];
                i++;
                for (k = 0; k < S[i]; k++, j++)
                {
                    weight[ply][j] = w[EVAL_S7[k] + offset];
                }
                offset += PS[i];
                i++;
                for (k = 0; k < S[i]; k++, j++)
                {
                    weight[ply][j] = w[EVAL_S6[k] + offset];
                }
                offset += PS[i];
                i++;
                for (k = 0; k < S[i]; k++, j++)
                {
                    weight[ply][j] = w[EVAL_S5[k] + offset];
                }
                offset += PS[i];
                i++;
                for (k = 0; k < S[i]; k++, j++)
                {
                    weight[ply][j] = w[EVAL_S4[k] + offset];
                }
                offset += PS[i];
                i++;
                weight[ply][j] = w[offset];
            }

            return weight;
        }

        static int board_get_square_color(B board, int x)
        {
            return 2 - 2 * (int)((board.bitB >> x) & 1) - (int)((board.bitW >> x) & 1);
        }

        public static int[] CreateFeature(B board)
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

        const int SCORE_MIN = -64;
        const int SCORE_MAX = 64;

        public static int Eval(B b)
        {
            byte[] w = weight[b.n_stone - 4];
            int[] f = CreateFeature(b);

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

            return score;
        }
    }
}
