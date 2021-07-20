using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;
using System.Buffers.Binary;

namespace OthelloAI
{
    public class MirroredBoards
    {
        public Board[] Boards = new Board[8];

        public MirroredBoards(Board org)
        {
            Boards[0] = org;
            Boards[1] = org.HorizontalMirrored();
            Boards[2] = org.VerticalMirrored();
            Boards[3] = Boards[1].VerticalMirrored();

            Boards[4] = org.Transposed();
            Boards[5] = Boards[4].HorizontalMirrored();
            Boards[6] = Boards[4].VerticalMirrored();
            Boards[7] = Boards[5].VerticalMirrored();
        }
    }

    public readonly struct Board
    {
        public const int BLACK = 1;
        public const int WHITE = -1;
        public const int NONE = 0;

        public const long InitB = 0x0000000810000000L;
        public const long InitW = 0x0000001008000000L;

        public readonly ulong bitB;
        public readonly ulong bitW;

        public readonly int stoneCount;

        public Board(Board source)
        {
            bitB = source.bitB;
            bitW = source.bitW;
            stoneCount = source.stoneCount;
        }

        public Board(ulong b, ulong w) : this(b, w, BitCount(b | w))
        {
        }

        public Board(ulong b, ulong w, int count)
        {
            bitB = b;
            bitW = w;
            stoneCount = count;
        }

        public Board(int[,] b)
        {
            stoneCount = 0;
            bitB = 0;
            bitW = 0;

            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    switch (b[x, y])
                    {
                        case BLACK:
                            bitB |= Mask(x, y);
                            stoneCount++;
                            break;

                        case WHITE:
                            bitW |= Mask(x, y);
                            stoneCount++;
                            break;
                    }
                }
            }
        }

        public Board HorizontalMirrored() => new Board(HorizontalMirror(bitB), HorizontalMirror(bitW), stoneCount);

        public static ulong HorizontalMirror(ulong x)
        {
            return BinaryPrimitives.ReverseEndianness(x);
        }

        public Board VerticalMirrored() => new Board(VerticalMirror(bitB), VerticalMirror(bitW), stoneCount);

        /*public static Board VerticalMirror(ulong b, ulong w)
        {
            Vector128<ulong> v = Vector128.Create(b, w);
            v = Sse2.Or(Sse2.And(Sse2.ShiftRightLogical(v, 1), Vector128.Create(0x5555555555555555UL)),
                    Sse2.And(Sse2.ShiftLeftLogical(v, 1), Vector128.Create(0xAAAAAAAAAAAAAAAAUL)));

            v = Sse2.Or(Sse2.And(Sse2.ShiftRightLogical(v, 2), Vector128.Create(0x3333333333333333UL)),
                    Sse2.And(Sse2.ShiftLeftLogical(v, 2), Vector128.Create(0xCCCCCCCCCCCCCCCCUL)));

            v = Sse2.Or(Sse2.And(Sse2.ShiftRightLogical(v, 4), Vector128.Create(0x0F0F0F0F0F0F0F0FUL)),
                    Sse2.And(Sse2.ShiftLeftLogical(v, 4), Vector128.Create(0xF0F0F0F0F0F0F0F0UL)));

            return new Board(Avx2.Extract(v, 0));
        }*/

        public static ulong VerticalMirror(ulong b)
        {
            b = ((b >> 1) & 0x5555555555555555UL) | ((b << 1) & 0xAAAAAAAAAAAAAAAAUL);
            b = ((b >> 2) & 0x3333333333333333UL) | ((b << 2) & 0xCCCCCCCCCCCCCCCCUL);
            b = ((b >> 4) & 0x0F0F0F0F0F0F0F0FUL) | ((b << 4) & 0xF0F0F0F0F0F0F0F0UL);

            return b;
        }

        public Board Transposed() => new Board(Transpose(bitB), Transpose(bitW), stoneCount);

        public static ulong TransposeAvx(ulong x)
        {
            Vector256<ulong> v = Avx2.ShiftLeftLogicalVariable(Vector256.Create(x), Vector256.Create(0ul, 1ul, 2ul, 3ul));
            return ((ulong)Avx2.MoveMask(v.AsByte()) << 32) | (uint)Avx2.MoveMask(Avx2.ShiftLeftLogical(v, 4).AsByte());
        }

        public static ulong Transpose(ulong x)
        {
            ulong t;
            ulong k1 = (0xaa00aa00aa00aa00);
            ulong k2 = (0xcccc0000cccc0000);
            ulong k4 = (0xf0f0f0f00f0f0f0f);
            t = x ^ (x << 36);
            x ^= k4 & (t ^ (x >> 36));
            t = k2 & (x ^ (x << 18));
            x ^= t ^ (t >> 18);
            t = k1 & (x ^ (x << 9));
            x ^= t ^ (t >> 9);
            return x;
        }

        public static int BitCount(ulong v)
        {
            return BitOperations.PopCount(v);
        }

        public static ulong LowestOneBit(ulong i)
        {
            return i & (~i + 1);
        }

        public static ulong Mask(int x, int y)
        {
            return Mask(To1dimPos(x, y));
        }

        public static ulong Mask(int x)
        {
            return 1UL << x;
        }

        public static int To1dimPos(int x, int y)
        {
            return x * 8 + y;
        }

        public int GetId(int x, int y)
        {
            return GetId(To1dimPos(x, y));
        }

        public int GetId(int i)
        {
            int result = (int)(bitB >> i) & 1;
            result += (int)((bitW >> i) & 1) * 2;
            return result;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(bitB, bitW);
        }

        public override bool Equals(object obj)
        {
            return (obj is Board b) && (b.bitB == bitB) && (b.bitW == bitW);
        }

        public ulong GetMoves() => GetMovesAvx2(bitB, bitW);

        public ulong GetOpponentMoves() => GetMovesAvx2(bitW, bitB);

        public ulong GetMoves(int stone) => stone switch
        {
            BLACK => GetMovesAvx2(bitB, bitW),
            WHITE => GetMovesAvx2(bitW, bitB),
            _ => 0,
        };

        public static ulong GetMoves(ulong player, ulong opponent)
        {
            ulong verticalMask = opponent & 0b0111111001111110011111100111111001111110011111100111111001111110UL;
            ulong empty = ~(player | opponent);

            ulong moves = GetMovesLR(player, opponent, 8); // 左, 右
            moves |= GetMovesLR(player, verticalMask, 1); // 上, 下
            moves |= GetMovesLR(player, verticalMask, 9); // 左上, 右下
            moves |= GetMovesLR(player, verticalMask, 7); // 左下, 右上

            return moves & empty;
        }

        private static ulong GetMovesLR(ulong player, ulong mask, int offset)
        {
            ulong m = ((player << offset) | (player >> offset)) & mask;
            m |= ((m << offset) | (m >> offset)) & mask;
            m |= ((m << offset) | (m >> offset)) & mask;
            m |= ((m << offset) | (m >> offset)) & mask;
            m |= ((m << offset) | (m >> offset)) & mask;
            m |= ((m << offset) | (m >> offset)) & mask;
            return (m << offset) | (m >> offset);
        }

        public static ulong GetMovesAvx2(ulong P, ulong O)
        {
            Vector256<ulong> PP, mask, moves, offset;
            Vector128<ulong> moves128;

            offset = Vector256.Create(7UL, 9UL, 8UL, 1UL);
            PP = Vector256.Create(P, P, P, P);
            mask = Avx2.And(Vector256.Create(O, O, O, O), Vector256.Create(0x7e7e7e7e7e7e7e7eUL, 0x7e7e7e7e7e7e7e7eUL, 0xffffffffffffffffUL, 0x7e7e7e7e7e7e7e7eUL));

            moves = Avx2.And(mask, Avx2.Or(Avx2.ShiftLeftLogicalVariable(PP, offset), Avx2.ShiftRightLogicalVariable(PP, offset)));
            moves = Avx2.Or(moves, Avx2.And(mask, Avx2.Or(Avx2.ShiftLeftLogicalVariable(moves, offset), Avx2.ShiftRightLogicalVariable(moves, offset))));
            moves = Avx2.Or(moves, Avx2.And(mask, Avx2.Or(Avx2.ShiftLeftLogicalVariable(moves, offset), Avx2.ShiftRightLogicalVariable(moves, offset))));
            moves = Avx2.Or(moves, Avx2.And(mask, Avx2.Or(Avx2.ShiftLeftLogicalVariable(moves, offset), Avx2.ShiftRightLogicalVariable(moves, offset))));
            moves = Avx2.Or(moves, Avx2.And(mask, Avx2.Or(Avx2.ShiftLeftLogicalVariable(moves, offset), Avx2.ShiftRightLogicalVariable(moves, offset))));
            moves = Avx2.Or(moves, Avx2.And(mask, Avx2.Or(Avx2.ShiftLeftLogicalVariable(moves, offset), Avx2.ShiftRightLogicalVariable(moves, offset))));

            moves = Avx2.Or(Avx2.ShiftLeftLogicalVariable(moves, offset), Avx2.ShiftRightLogicalVariable(moves, offset));

            moves128 = Sse2.Or(Avx2.ExtractVector128(moves, 0), Avx2.ExtractVector128(moves, 1));
            return (Sse2.UnpackHigh(moves128, moves128).ToScalar() | moves128.ToScalar()) & ~(P | O);
        }

        public static ulong GetMovesAvx(ulong P, ulong O)
        {
            Vector256<ulong> PP, mOO, MM, flip_l, flip_r, pre_l, pre_r, shift2;
            Vector128<ulong> M;
            Vector256<ulong> shift1897 = Vector256.Create(7UL, 9UL, 8UL, 1UL);
            Vector256<ulong> mflipH = Vector256.Create(0x7e7e7e7e7e7e7e7eUL, 0x7e7e7e7e7e7e7e7eUL, 0xffffffffffffffffUL, 0x7e7e7e7e7e7e7e7eUL);

            PP = Vector256.Create(P, P, P, P);
            mOO = Avx2.And(Vector256.Create(O, O, O, O), mflipH);

            flip_l = Avx2.And(mOO, Avx2.ShiftLeftLogicalVariable(PP, shift1897));
            flip_r = Avx2.And(mOO, Avx2.ShiftRightLogicalVariable(PP, shift1897));
            flip_l = Avx2.Or(flip_l, Avx2.And(mOO, Avx2.ShiftLeftLogicalVariable(flip_l, shift1897)));
            flip_r = Avx2.Or(flip_r, Avx2.And(mOO, Avx2.ShiftRightLogicalVariable(flip_r, shift1897)));

            pre_l = Avx2.And(mOO, Avx2.ShiftLeftLogicalVariable(mOO, shift1897));
            pre_r = Avx2.ShiftRightLogicalVariable(pre_l, shift1897);
            shift2 = Avx2.Add(shift1897, shift1897);

            flip_l = Avx2.Or(flip_l, Avx2.And(pre_l, Avx2.ShiftLeftLogicalVariable(flip_l, shift2)));
            flip_r = Avx2.Or(flip_r, Avx2.And(pre_r, Avx2.ShiftRightLogicalVariable(flip_r, shift2)));
            flip_l = Avx2.Or(flip_l, Avx2.And(pre_l, Avx2.ShiftLeftLogicalVariable(flip_l, shift2)));
            flip_r = Avx2.Or(flip_r, Avx2.And(pre_r, Avx2.ShiftRightLogicalVariable(flip_r, shift2)));
            MM = Avx2.ShiftLeftLogicalVariable(flip_l, shift1897);
            MM = Avx2.Or(MM, Avx2.ShiftRightLogicalVariable(flip_r, shift1897));

            M = Sse2.Or(Avx2.ExtractVector128(MM, 0), Avx2.ExtractVector128(MM, 1));
            M = Sse2.Or(M, Sse2.UnpackHigh(M, M));
            return M.ToScalar() & ~(P | O); // mask with empties
        }

        public Board ColorFliped()
        {
            return new Board(bitW, bitB, stoneCount);
        }

        public int GetReversedCountOnLastMove()
        {
            int count = ReverseUtil.GetReversedCountOnLastMove(bitB, bitW);
            if(count > 0)
            {
                return count;
            }
            return -ReverseUtil.GetReversedCountOnLastMove(bitW, bitB);
        }

        public Board Reversed(ulong move)
        {
            ulong reversed = ReverseUtil.ReverseAvx(move, bitB, bitW);
            return new Board(bitW ^ reversed, bitB ^ (move | reversed), stoneCount + 1);
        }

        public Board Reversed(ulong move, int stone)
        {
            ulong reversed;

            switch (stone)
            {
                case BLACK:
                    reversed = ReverseUtil.ReverseAvx(move, bitB, bitW);
                    return new Board(bitB ^ (move | reversed), bitW ^ reversed, stoneCount + 1);

                case WHITE:
                    reversed = ReverseUtil.ReverseAvx(move, bitW, bitB);
                    return new Board(bitB ^ reversed, bitW ^ (move | reversed), stoneCount + 1);
            }
            return this;
        }

        public int GetStoneCount() => BitCount(bitB);

        public int GetStoneCount(int s) => s switch
        {
            BLACK => BitCount(bitB),
            WHITE => BitCount(bitW),
            NONE => BitCount(~(bitB | bitW)),
            _ => -1,
        };

        public int GetStoneCountGap()
        {
            return (BitCount(bitB) - BitCount(bitW));
        }

        public int GetStoneCountGap(int s)
        {
            return s * (BitCount(bitB) - BitCount(bitW));
        }

        public bool IsWon(int s)
        {
            return GetStoneCountGap(s) > 0;
        }

        public static ulong NextMove(ulong moves)
        {
            return LowestOneBit(moves);
        }

        public static ulong RemoveMove(ulong moves, ulong move)
        {
            return moves ^ move;
        }

        public void print()
        {
            Console.WriteLine("    0   1   2   3   4   5   6   7");
            Console.WriteLine("  +---+---+---+---+---+---+---+---+");
            for (int y = 0; y < 8; y++)
            {
                Console.Write(y + " |");
                for (int x = 0; x < 8; x++)
                {
                    switch (GetId(x, y))
                    {
                        case 1:
                            Console.Write(" X |");
                            break;

                        case 2:
                            Console.Write(" O |");
                            break;

                        case 0:
                            Console.Write("   |");
                            break;

                        default:
                            Console.Write(" " + GetId(x, y) + " |");
                            break;
                    }
                }
                Console.WriteLine();
                Console.WriteLine("  +---+---+---+---+---+---+---+---+");
            }
        }
    }
}
