using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.X86;

using OthelloAI.Patterns;

namespace OthelloAI
{
    public abstract class BoardHasher
    {
        public int HashLength { get; }
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

            foreach(int pos in Positions)
            {
                hash *= 3;
                hash += (int)((board.bitB >> pos) & 1);
                hash += (int)((board.bitB >> pos) & 1) * 2;
            }
            return hash;
        }

        public Board FromHash(int hash)
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
    }

    public class BoardHasherMask : BoardHasher
    {
        public ulong Mask { get; }
        public override int[] Positions { get; }

        public override int HashByPEXT(ulong b) => (int)Bmi2.X64.ParallelBitExtract(b, Mask);

        public BoardHasherMask(ulong mask)
        {
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
        public ulong Mask { get; }
        public int Line { get; }
        public override int[] Positions { get; } = Enumerable.Range(0, 8).ToArray();

        public override int HashByPEXT(ulong b) => (int)((b >> (Line * 8)) & 0xFF);

        public BoardHasherLine1(int line)
        {
            Line = line;

            for(int i = 0; i < 8; i++)
            {
                Positions[i] += line * 8;
            }
        }
    }
}
