using OthelloAI.Patterns;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.X86;

namespace OthelloAI
{
    public abstract class BoardHasher
    {
        public abstract int HashLength { get; }
        public abstract int[] Positions { get; }
        public int HashBinByPEXT(in Board b) => HashByPEXT(b.bitB) | (HashByPEXT(b.bitW) << HashLength);
        public int HashTerByPEXT(in Board b) => BinTerUtil.ConvertBinToTer(HashByPEXT(b.bitB), HashLength) + 2 * BinTerUtil.ConvertBinToTer(HashByPEXT(b.bitW), HashLength);

        public int HashByPEXT(in Board b, NAry ary) => ary switch
        {
            NAry.BIN => HashBinByPEXT(b),
            NAry.TER => HashTerByPEXT(b),
            _ => throw new NotImplementedException(),
        };

        public abstract int HashByPEXT(ulong b);

        public int HashByScanning(Board board, int number)
        {
            throw new NotImplementedException();

            int hash = 0;

            foreach (int pos in Positions)
            {
                hash *= 3;
                hash += (int)((board.bitB >> pos) & 1);
                hash += (int)((board.bitB >> pos) & 1) * 2;
            }
            return hash;
        }

        public int ConvertStateToHash(int i, NAry nary)
        {
            switch (nary)
            {
                case NAry.BIN:
                    (int b1, int b2) = BinTerUtil.ConvertTerToBinPair(i, HashLength);
                    return b1 | (b2 << HashLength);

                case NAry.TER:
                    return i;

                default:
                    throw new NotImplementedException();
            }
        }

        public int FlipHash(int hash, NAry nary) => nary switch
        {
            NAry.BIN => FlipBinHash(hash),
            NAry.TER => FlipTerHash(hash),
            _ => throw new NotImplementedException()
        };

        public int FlipBinHash(int hash) => (hash >> HashLength) | ((hash & ((1 << HashLength) - 1)) << HashLength);

        public int FlipTerHash(int hash)
        {
            int result = 0;

            for (int i = 0; i < HashLength; i++)
            {
                int s = hash % 3;
                hash /= 3;
                s = s == 0 ? 0 : (s == 1 ? 2 : 1);
                result += s * BinTerUtil.POW3_TABLE[i];
            }
            return result;
        }

        public Board FromHash(int hash, NAry nary) => nary switch
        {
            NAry.BIN => FromBinHash(hash),
            NAry.TER => FromTerHash(hash),
            _ => throw new NotImplementedException()
        };

        public Board FromTerHash(int hash)
        {
            ulong b = 0;
            ulong w = 0;

            for (int i = 0; i < HashLength; i++)
            {
                int id = hash % 3;
                switch (id)
                {
                    case 1:
                        b |= Board.Mask(Positions[i]);
                        break;

                    case 2:
                        w |= Board.Mask(Positions[i]);
                        break;
                }
                hash /= 3;
            }
            return new Board(b, w);
        }

        public Board FromBinHash(int hash)
        {
            int hash_b = hash & ((1 << HashLength) - 1);
            int hash_w = hash >> HashLength;

            ulong b = 0;
            ulong w = 0;

            for (int i = 0; i < HashLength; i++)
            {
                if (((hash_b >> i) & 1) == 1)
                {
                    b |= Board.Mask(Positions[i]);
                }
                else if (((hash_w >> i) & 1) == 1)
                {
                    w |= Board.Mask(Positions[i]);
                }

                hash >>= 1;
            }
            return new Board(b, w);
        }
    }

    public class BoardHasherMask : BoardHasher
    {
        public ulong Mask { get; }
        public override int HashLength { get; }
        public override int[] Positions { get; }

        public override int HashByPEXT(ulong b) => (int)Bmi2.X64.ParallelBitExtract(b, Mask);

        public BoardHasherMask(ulong mask)
        {
            Mask = mask;
            HashLength = Board.BitCount(mask);

            var list = new List<int>();

            for (int i = 0; i < 64; i++)
            {
                if (((mask >> i) & 1) != 0)
                    list.Add(i);
            }
            Positions = list.ToArray();
        }
    }

    public class BoardHasherLine1 : BoardHasher
    {
        public int Line { get; }
        public override int HashLength => 8;
        public override int[] Positions { get; } = Enumerable.Range(0, 8).ToArray();

        public override int HashByPEXT(ulong b) => (int)((b >> (Line * 8)) & 0xFF);

        public BoardHasherLine1(int line)
        {
            Line = line;

            for (int i = 0; i < 8; i++)
            {
                Positions[i] += line * 8;
            }
        }
    }
}
