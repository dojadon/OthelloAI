using OthelloAI.Patterns;
using System.Runtime.Intrinsics.X86;
using System.Collections.Generic;

namespace OthelloAI
{
    public interface IBoardIndexer
    {
        public int Hash(in Board board);
        public IndexingType IndexingType { get; }
        public int [] Positions { get; }
    }

    public abstract class BoardIndexerMask : IBoardIndexer
    {
        public ulong Mask { get; }
        public int HashLength { get; }
        public abstract IndexingType IndexingType { get; }

        public int[] Positions { get; }

        public BoardIndexerMask(ulong mask)
        {
            var list = new List<int>();

            for (int i = 0; i < 64; i++)
            {
                if (((mask >> i) & 1) != 0)
                    list.Add(i);
            }
            Positions = list.ToArray();
        }

        public abstract int Hash(in Board board);
    }

    public class BoardIndexerMaskTer : BoardIndexerMask
    {
        public override IndexingType IndexingType => IndexingType.TER;

        public BoardIndexerMaskTer(ulong mask) : base(mask)
        {
        }

        public override int Hash(in Board board)
        {
            int b1 = (int)Bmi2.X64.ParallelBitExtract(board.bitB, Mask);
            int b2 = (int)Bmi2.X64.ParallelBitExtract(board.bitW, Mask);

            return BinTerUtil.ConvertBinToTer(b1, HashLength) + 2 * BinTerUtil.ConvertBinToTer(b2, HashLength);
        }
    }

    public class BoardIndexerMaskBin : BoardIndexerMask
    {
        public override IndexingType IndexingType => IndexingType.BIN;

        public BoardIndexerMaskBin(ulong mask) : base(mask)
        {
        }

        public override int Hash(in Board board)
        {
            return (int)(Bmi2.X64.ParallelBitExtract(board.bitB, Mask) | (Bmi2.X64.ParallelBitExtract(board.bitW, Mask) << HashLength));
        }
    }
}
