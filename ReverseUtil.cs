using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Diagnostics;

namespace OthelloAI
{
    static class ReverseUtil
    {
        public static readonly int[,] REVERSED_TABLE = CreateReversedTable();

        public static int[,] CreateReversedTable()
        {
            int[,] table = new int[8, 0x100];

            for (int x = 0; x < 8; x++)
            {
                int move = 1 << x;

                for (int i = 0; i < 0x100; i++)
                {
                    if ((i & move) != 0)
                        continue;

                    int left = 0;
                    while ((i & (move << (left + 1))) != 0)
                    {
                        left++;
                    }

                    if (x + left >= 7)
                        left = 0;

                    int right = 0;
                    while ((i & (move >> (right + 1))) != 0)
                    {
                        right++;
                    }

                    if (x - right <= 0)
                        right = 0;

                    table[x, i] = left + right;

                    /*string s = Convert.ToString(i, 2).PadLeft(8, '0');
                    Console.WriteLine($"{s.Remove(7 - x, 1).Insert(7 - x, "X")} : {left}, {right}");*/
                }
            }
            return table;
        }

        public static ulong[, ] MASK_TABLE ={
            { 0x0000000000000001UL, 0x8040201008040201UL, 0x0101010101010101UL, 0x81412111090503ffUL },
            { 0x0000000000000102UL, 0x0080402010080402UL, 0x0202020202020202UL, 0x02824222120a07ffUL },
            { 0x0000000000010204UL, 0x0000804020100804UL, 0x0404040404040404UL, 0x0404844424150effUL },
            { 0x0000000001020408UL, 0x0000008040201008UL, 0x0808080808080808UL, 0x08080888492a1cffUL },
            { 0x0000000102040810UL, 0x0000000080402010UL, 0x1010101010101010UL, 0x10101011925438ffUL },
            { 0x0000010204081020UL, 0x0000000000804020UL, 0x2020202020202020UL, 0x2020212224a870ffUL },
            { 0x0001020408102040UL, 0x0000000000008040UL, 0x4040404040404040UL, 0x404142444850e0ffUL },
            { 0x0102040810204080UL, 0x0000000000000080UL, 0x8080808080808080UL, 0x8182848890a0c0ffUL },
            { 0x0000000000000102UL, 0x4020100804020104UL, 0x0101010101010101UL, 0x412111090503ff03UL },
            { 0x0000000000010204UL, 0x8040201008040201UL, 0x0202020202020202UL, 0x824222120a07ff07UL },
            { 0x0000000001020408UL, 0x0080402010080402UL, 0x0404040404040404UL, 0x04844424150eff0eUL },
            { 0x0000000102040810UL, 0x0000804020100804UL, 0x0808080808080808UL, 0x080888492a1cff1cUL },
            { 0x0000010204081020UL, 0x0000008040201008UL, 0x1010101010101010UL, 0x101011925438ff38UL },
            { 0x0001020408102040UL, 0x0000000080402010UL, 0x2020202020202020UL, 0x20212224a870ff70UL },
            { 0x0102040810204080UL, 0x0000000000804020UL, 0x4040404040404040UL, 0x4142444850e0ffe0UL },
            { 0x0204081020408001UL, 0x0000000000008040UL, 0x8080808080808080UL, 0x82848890a0c0ffc0UL },
            { 0x0000000000010204UL, 0x201008040201000aUL, 0x0101010101010101UL, 0x2111090503ff0305UL },
            { 0x0000000001020408UL, 0x4020100804020101UL, 0x0202020202020202UL, 0x4222120a07ff070aUL },
            { 0x0000000102040810UL, 0x8040201008040201UL, 0x0404040404040404UL, 0x844424150eff0e15UL },
            { 0x0000010204081020UL, 0x0080402010080402UL, 0x0808080808080808UL, 0x0888492a1cff1c2aUL },
            { 0x0001020408102040UL, 0x0000804020100804UL, 0x1010101010101010UL, 0x1011925438ff3854UL },
            { 0x0102040810204080UL, 0x0000008040201008UL, 0x2020202020202020UL, 0x212224a870ff70a8UL },
            { 0x0204081020408001UL, 0x0000000080402010UL, 0x4040404040404040UL, 0x42444850e0ffe050UL },
            { 0x0408102040800003UL, 0x0000000000804020UL, 0x8080808080808080UL, 0x848890a0c0ffc0a0UL },
            { 0x0000000001020408UL, 0x1008040201000016UL, 0x0101010101010101UL, 0x11090503ff030509UL },
            { 0x0000000102040810UL, 0x2010080402010005UL, 0x0202020202020202UL, 0x22120a07ff070a12UL },
            { 0x0000010204081020UL, 0x4020100804020101UL, 0x0404040404040404UL, 0x4424150eff0e1524UL },
            { 0x0001020408102040UL, 0x8040201008040201UL, 0x0808080808080808UL, 0x88492a1cff1c2a49UL },
            { 0x0102040810204080UL, 0x0080402010080402UL, 0x1010101010101010UL, 0x11925438ff385492UL },
            { 0x0204081020408001UL, 0x0000804020100804UL, 0x2020202020202020UL, 0x2224a870ff70a824UL },
            { 0x0408102040800003UL, 0x0000008040201008UL, 0x4040404040404040UL, 0x444850e0ffe05048UL },
            { 0x0810204080000007UL, 0x0000000080402010UL, 0x8080808080808080UL, 0x8890a0c0ffc0a090UL },
            { 0x0000000102040810UL, 0x080402010000002eUL, 0x0101010101010101UL, 0x090503ff03050911UL },
            { 0x0000010204081020UL, 0x100804020100000dUL, 0x0202020202020202UL, 0x120a07ff070a1222UL },
            { 0x0001020408102040UL, 0x2010080402010003UL, 0x0404040404040404UL, 0x24150eff0e152444UL },
            { 0x0102040810204080UL, 0x4020100804020101UL, 0x0808080808080808UL, 0x492a1cff1c2a4988UL },
            { 0x0204081020408002UL, 0x8040201008040201UL, 0x1010101010101010UL, 0x925438ff38549211UL },
            { 0x0408102040800005UL, 0x0080402010080402UL, 0x2020202020202020UL, 0x24a870ff70a82422UL },
            { 0x081020408000000bUL, 0x0000804020100804UL, 0x4040404040404040UL, 0x4850e0ffe0504844UL },
            { 0x1020408000000017UL, 0x0000008040201008UL, 0x8080808080808080UL, 0x90a0c0ffc0a09088UL },
            { 0x0000010204081020UL, 0x040201000000005eUL, 0x0101010101010101UL, 0x0503ff0305091121UL },
            { 0x0001020408102040UL, 0x080402010000001dUL, 0x0202020202020202UL, 0x0a07ff070a122242UL },
            { 0x0102040810204080UL, 0x100804020100000bUL, 0x0404040404040404UL, 0x150eff0e15244484UL },
            { 0x0204081020408001UL, 0x2010080402010003UL, 0x0808080808080808UL, 0x2a1cff1c2a498808UL },
            { 0x0408102040800003UL, 0x4020100804020101UL, 0x1010101010101010UL, 0x5438ff3854921110UL },
            { 0x081020408000000eUL, 0x8040201008040201UL, 0x2020202020202020UL, 0xa870ff70a8242221UL },
            { 0x102040800000001dUL, 0x0080402010080402UL, 0x4040404040404040UL, 0x50e0ffe050484442UL },
            { 0x204080000000003bUL, 0x0000804020100804UL, 0x8080808080808080UL, 0xa0c0ffc0a0908884UL },
            { 0x0001020408102040UL, 0x02010000000000beUL, 0x0101010101010101UL, 0x03ff030509112141UL },
            { 0x0102040810204080UL, 0x040201000000003dUL, 0x0202020202020202UL, 0x07ff070a12224282UL },
            { 0x0204081020408001UL, 0x080402010000001bUL, 0x0404040404040404UL, 0x0eff0e1524448404UL },
            { 0x0408102040800003UL, 0x1008040201000007UL, 0x0808080808080808UL, 0x1cff1c2a49880808UL },
            { 0x0810204080000007UL, 0x2010080402010003UL, 0x1010101010101010UL, 0x38ff385492111010UL },
            { 0x102040800000000fUL, 0x4020100804020101UL, 0x2020202020202020UL, 0x70ff70a824222120UL },
            { 0x204080000000003eUL, 0x8040201008040201UL, 0x4040404040404040UL, 0xe0ffe05048444241UL },
            { 0x408000000000007dUL, 0x0080402010080402UL, 0x8080808080808080UL, 0xc0ffc0a090888482UL },
            { 0x0102040810204080UL, 0x010000000000027eUL, 0x0101010101010101UL, 0xff03050911214181UL },
            { 0x0204081020408001UL, 0x020100000000007dUL, 0x0202020202020202UL, 0xff070a1222428202UL },
            { 0x0408102040800003UL, 0x040201000000003bUL, 0x0404040404040404UL, 0xff0e152444840404UL },
            { 0x0810204080000007UL, 0x0804020100000017UL, 0x0808080808080808UL, 0xff1c2a4988080808UL },
            { 0x102040800000000fUL, 0x1008040201000007UL, 0x1010101010101010UL, 0xff38549211101010UL },
            { 0x204080000000001fUL, 0x2010080402010003UL, 0x2020202020202020UL, 0xff70a82422212020UL },
            { 0x408000000000003fUL, 0x4020100804020101UL, 0x4040404040404040UL, 0xffe0504844424140UL },
            { 0x800000000000017eUL, 0x8040201008040201UL, 0x8080808080808080UL, 0xffc0a09088848281UL }
        };

        public static int CountOnLastMove(ulong p, ulong o)
        {
            ulong move = ~(p | o);
            int pos = BitOperations.TrailingZeroCount(move);
            int x = pos / 8;
            int y = pos & 7;

            o &= MASK_TABLE[pos, 3];

            int count = REVERSED_TABLE[y, (byte)(o >> (pos & 0x38))];
            count += REVERSED_TABLE[x, Bmi2.X64.ParallelBitExtract(o, MASK_TABLE[pos, 0])];
            count += REVERSED_TABLE[x, Bmi2.X64.ParallelBitExtract(o, MASK_TABLE[pos, 1])];
            count += REVERSED_TABLE[x, Bmi2.X64.ParallelBitExtract(o, MASK_TABLE[pos, 2])];

            return count;
        }

        public static ulong ReverseAvx(ulong move, ulong p, ulong o)
        {
            Vector256<ulong> PP, mask, reversed, flip_l, flip_r, flags;
            Vector128<ulong> reversed128;
            Vector256<ulong> offset = Vector256.Create(7UL, 9UL, 8UL, 1UL);
            Vector256<ulong> move_v = Vector256.Create(move);

            PP = Vector256.Create(p);
            mask = Avx2.And(Vector256.Create(o), Vector256.Create(0x7e7e7e7e7e7e7e7eUL, 0x7e7e7e7e7e7e7e7eUL, 0xffffffffffffffffUL, 0x7e7e7e7e7e7e7e7eUL));

            flip_l = Avx2.And(mask, Avx2.ShiftLeftLogicalVariable(move_v, offset));
            flip_l = Avx2.Or(flip_l, Avx2.And(mask, Avx2.ShiftLeftLogicalVariable(flip_l, offset)));
            flip_l = Avx2.Or(flip_l, Avx2.And(mask, Avx2.ShiftLeftLogicalVariable(flip_l, offset)));
            flip_l = Avx2.Or(flip_l, Avx2.And(mask, Avx2.ShiftLeftLogicalVariable(flip_l, offset)));
            flip_l = Avx2.Or(flip_l, Avx2.And(mask, Avx2.ShiftLeftLogicalVariable(flip_l, offset)));
            flip_l = Avx2.Or(flip_l, Avx2.And(mask, Avx2.ShiftLeftLogicalVariable(flip_l, offset)));

            flags = Avx2.And(PP, Avx2.ShiftLeftLogicalVariable(flip_l, offset));
            flip_l = Avx2.And(flip_l, Avx2.Xor(Vector256.Create(0xffffffffffffffffUL), Avx2.CompareEqual(flags, Vector256.Create(0UL))));

            flip_r = Avx2.And(mask, Avx2.ShiftRightLogicalVariable(move_v, offset));
            flip_r = Avx2.Or(flip_r, Avx2.And(mask, Avx2.ShiftRightLogicalVariable(flip_r, offset)));
            flip_r = Avx2.Or(flip_r, Avx2.And(mask, Avx2.ShiftRightLogicalVariable(flip_r, offset)));
            flip_r = Avx2.Or(flip_r, Avx2.And(mask, Avx2.ShiftRightLogicalVariable(flip_r, offset)));
            flip_r = Avx2.Or(flip_r, Avx2.And(mask, Avx2.ShiftRightLogicalVariable(flip_r, offset)));
            flip_r = Avx2.Or(flip_r, Avx2.And(mask, Avx2.ShiftRightLogicalVariable(flip_r, offset)));

            flags = Avx2.And(PP, Avx2.ShiftRightLogicalVariable(flip_r, offset));
            flip_r = Avx2.And(flip_r, Avx2.Xor(Vector256.Create(0xffffffffffffffffUL), Avx2.CompareEqual(flags, Vector256.Create(0UL))));

            reversed = Avx2.Or(flip_l, flip_r);

            reversed128 = Sse2.Or(Avx2.ExtractVector128(reversed, 0), Avx2.ExtractVector128(reversed, 1));
            reversed128 = Sse2.Or(reversed128, Sse2.UnpackHigh(reversed128, reversed128));
            return reversed128.ToScalar();
        }

        public static ulong Reverse(ulong move, ulong player, ulong opponent)
        {
            ulong verticalMask = opponent & 0x7e7e7e7e7e7e7e7eUL;

            ulong reversed = 0;
            reversed |= GetReversedL(move, player, 8, opponent);
            reversed |= GetReversedR(move, player, 8, opponent);
            reversed |= GetReversedL(move, player, 1, verticalMask);
            reversed |= GetReversedR(move, player, 1, verticalMask);
            reversed |= GetReversedL(move, player, 9, verticalMask);
            reversed |= GetReversedR(move, player, 9, verticalMask);
            reversed |= GetReversedL(move, player, 7, verticalMask);
            reversed |= GetReversedR(move, player, 7, verticalMask);

            return reversed;
        }

        private static ulong GetReversedL(ulong move, ulong player, int offset, ulong mask)
        {
            ulong r = (move << offset) & mask;
            r |= (r << offset) & mask;
            r |= (r << offset) & mask;
            r |= (r << offset) & mask;
            r |= (r << offset) & mask;
            r |= (r << offset) & mask;

            if (((r << offset) & player) != 0)
            {
                return r;
            }

            return 0;
        }

        private static ulong GetReversedR(ulong move, ulong player, int offset, ulong mask)
        {
            ulong r = (move >> offset) & mask;
            r |= (r >> offset) & mask;
            r |= (r >> offset) & mask;
            r |= (r >> offset) & mask;
            r |= (r >> offset) & mask;
            r |= (r >> offset) & mask;

            if (((r >> offset) & player) != 0)
            {
                return r;
            }

            return 0;
        }
    }
}
