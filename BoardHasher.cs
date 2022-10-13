using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.X86;

namespace OthelloAI
{
    public enum HashType
    {
        BIN, TER
    }

    public abstract class BoardHasher
    {
        public abstract HashType HashType { get; }

        public int ArrayLength => HashType switch
        {
            HashType.BIN => (int)Math.Pow(2, HashLength * 2),
            HashType.TER => (int)Math.Pow(3, HashLength),
            _ => throw new NotImplementedException(),
        };

        public int NumOfStates => (int)Math.Pow(3, HashLength);

        public abstract int HashLength { get; }
        public abstract int[] Positions { get; }

        public abstract int Hash(in Board b);

        public abstract uint ConvertStateToHash(int i);

        public abstract int FlipHash(int hash);

        public abstract Board FromHash(uint hash);
    }

    public abstract class BoardHasherBin : BoardHasher
    {
        public override HashType HashType => HashType.BIN;

        public override int Hash(in Board b) => Hash(b.bitB) | (Hash(b.bitW) << HashLength);

        public abstract int Hash(ulong b);

        public override uint ConvertStateToHash(int i)
        {
            (uint b1, uint b2) = BinTerUtil.ConvertTerToBinPair(i, HashLength);
            return b1 | (b2 << HashLength);
        }

        public override int FlipHash(int hash)
        {
            return (hash >> HashLength) | ((hash & ((1 << HashLength) - 1)) << HashLength);
        }

        public override Board FromHash(uint hash)
        {
            uint hash_b = hash & ((1u << HashLength) - 1u);
            uint hash_w = hash >> HashLength;

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

    public abstract class BoardHasherTer : BoardHasher
    {
        public override HashType HashType => HashType.TER;

        public override uint ConvertStateToHash(int i)
        {
            return (uint) i;
        }

        public override int FlipHash(int hash)
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

        public override Board FromHash(uint hash)
        {
            ulong b = 0;
            ulong w = 0;

            for (int i = 0; i < HashLength; i++)
            {
                switch (hash % 3)
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

    public class BoardhasherTerFromBin : BoardHasherTer
    {
        public BoardHasherBin HasherBin { get; }

        public override int HashLength => HasherBin.HashLength;

        public override int[] Positions => HasherBin.Positions;

        public override int Hash(in Board b) => BinTerUtil.ConvertBinToTer(HasherBin.Hash(b.bitB), HashLength) + 2 * BinTerUtil.ConvertBinToTer(HasherBin.Hash(b.bitW), HashLength);
    }

    public class BoardHasherScanning : BoardHasherTer
    {
        public override int[] Positions { get; }
        public override int HashLength => Positions.Length;

        public BoardHasherScanning(int[] positions)
        {
            Positions = positions;
        }

        public override int Hash(in Board board)
        {
            int hash = 0;

            for (int i = 0; i < Positions.Length; i++)
            {
                int pos = Positions[i];

                hash *= 3;
                hash += (int)((board.bitB >> pos) & 1);
                hash += (int)((board.bitW >> pos) & 1) * 2;
            }
            return hash;
        }
    }

    public class BoardHasherScanning2 : BoardHasherTer
    {
        public override int[] Positions { get; }
        public override int HashLength => Positions.Length;

        public int Pos1 { get; }
        public int Pos2 { get; }

        public BoardHasherScanning2(int pos1, int pos2)
        {
            Positions = new int[] { pos1, pos2};

            Pos1 = pos1;
            Pos2 = pos2;
        }

        public override int Hash(in Board board)
        {
            return (int)((board.bitB >> Pos1) & 1) * 3 + (int)((board.bitW >> Pos1) & 1) * 6 + (int)((board.bitB >> Pos2) & 1) + (int)((board.bitW >> Pos2) & 1) * 2;
        }
    }

    public class BoardHasherMask : BoardHasherBin
    {
        public ulong Mask { get; }
        public override int HashLength { get; }
        public override int[] Positions { get; }

        public override int Hash(ulong b) => (int)Bmi2.X64.ParallelBitExtract(b, Mask);

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
            list.Reverse();

            Positions = list.ToArray();
        }
    }

    public class BoardHasherLine1 : BoardHasherBin
    {
        public int Line { get; }
        public override int HashLength => 8;
        public override int[] Positions { get; } = Enumerable.Range(0, 8).ToArray();

        public override int Hash(ulong b) => (int)((b >> (Line * 8)) & 0xFF);

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
