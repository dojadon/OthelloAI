using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;
using System.Buffers.Binary;
using System.Diagnostics;

class Player
{
    public static readonly Stopwatch Stopwatch = new Stopwatch();

    static void Main(string[] args)
    {
        InitTable();

        int id = int.Parse(Console.ReadLine()); // id of your player.
        int boardSize = int.Parse(Console.ReadLine());

        string row_labels = "abcdefgh";

        // game loop
        while (true)
        {
            ulong b = 0;
            ulong w = 0;

            for (int i = 0; i < boardSize; i++)
            {
                string line = Console.ReadLine(); // rows from top to bottom (viewer perspective).

                for (int j = 0; j < boardSize; j++)
                {
                    int pos = j * 8 + i;

                    switch (line[j])
                    {
                        case '0':
                            b |= 1UL << pos;
                            break;

                        case '1':
                            w |= 1UL << pos;
                            break;
                    }
                }
            }
            int actionCount = int.Parse(Console.ReadLine()); // number of legal actions for this turn.
            for (int i = 0; i < actionCount; i++)
            {
                string action = Console.ReadLine(); // the action
            }

            Stopwatch.Restart();

            Board board = id == 0 ? new Board(b, w) : new Board(w, b);

            ulong move;
            if (board.stoneCount > 48)
            {
                move = NegascoutRootDefinite(board, GetSearchDepth(board));
            }
            else
            {
                move = NegascoutRoot(board, GetSearchDepth(board));
            }
            (int x, int y) = Board.ToPos(move);

            Stopwatch.Stop();
            Console.Error.WriteLine("Taken Time : " + Stopwatch.ElapsedMilliseconds);

            Console.WriteLine("" + row_labels[x] + (y + 1));
        }
    }

    protected static int GetSearchDepth(Board board)
    {
        if (board.stoneCount > 48)
        {
            //Console.WriteLine("完全読み開始");
            return 64 - board.stoneCount + 1;
        }
        else if (board.stoneCount > 47)
        {
            return 10;
        }
        else if (board.stoneCount > 42)
        {
            return 9;
        }
        else if (board.stoneCount > 40)
        {
            return 8;
        }
        else if (board.stoneCount > 10)
        {
            return 8;
        }
        else
        {
            return 7;
        }
    }

    public static int GetBinHash2(in Board b)
    {
        return (int)(Bmi2.X64.ParallelBitExtract(b.bitB, 0x70703) | (Bmi2.X64.ParallelBitExtract(b.bitW, 0x70703) << 10));
    }

    public static int GetBinHash(in Board b)
    {
        return (int)((b.bitB & 0xFF) | ((b.bitW & 0xFF) << 10));
    }

    public static readonly int[] TERNARY_TABLE = new int[1 << 20];

    public static int GetHash(in Board board)
    {
        return TERNARY_TABLE[GetBinHash(board)];
    }

    public static int GetHash2(in Board board)
    {
        return TERNARY_TABLE[GetBinHash2(board)];
    }

    public static void InitTable()
    {
        static int Convert(int b)
        {
            int result = 0;

            for (int i = 0; i < 10; i++)
            {
                result += ((b >> i) & 1) * POW3_TABLE[i];
            }
            return result;
        }

        for (int i = 0; i < TERNARY_TABLE.Length; i++)
        {
            TERNARY_TABLE[i] = Convert(i) + Convert(i >> 10) * 2;
        }
    }

    public static float Eval(Board board)
    {
        Board.CreateRotatedBoards(board, out Board b1, out Board b2, out Board b3, out Board b4);

        byte[] eval1 = Evaluation.array1;
        byte[] eval2 = Evaluation.array2;

        if (board.stoneCount < 30)
        {
            return eval1[GetHash(board)] + eval1[GetHash(b1)] + eval1[GetHash(b2)] + eval1[GetHash(b3)]
            + eval2[GetHash2(board)] + eval2[GetHash2(b1)] + eval2[GetHash2(b2)] + eval2[GetHash2(b4)] - 1024
            + (Board.BitCount(board.GetMoves()) - Board.BitCount(board.GetOpponentMoves())) * 64
            + (Board.BitCount(board.bitB & 0x8100000000000081UL) - Board.BitCount(board.bitW & 0x8100000000000081UL)) * 128
            + (Board.BitCount(board.bitW) - Board.BitCount(board.bitB)) * 16;
        }
        else
        {
            return eval1[GetHash(board)] + eval1[GetHash(b1)] + eval1[GetHash(b2)] + eval1[GetHash(b3)]
            + eval2[GetHash2(board)] + eval2[GetHash2(b1)] + eval2[GetHash2(b2)] + eval2[GetHash2(b4)] - 1024;
        }
    }

    protected static float EvalFinishedGame(Board board)
    {
        return board.GetStoneCountGap() * 10000;
    }

    protected static float EvalLastMove(Board board)
    {
        return (board.GetStoneCountGap() + board.GetReversedCountOnLastMove()) * 10000;
    }

    public static ulong NegascoutRootDefinite(Board board, int depth)
    {
        Move root = new Move(board);

        if (root.count <= 1)
        {
            return root.moves;
        }

        Move[] array = CreateMoveArray(root.reversed, root.moves, root.count);

        Array.Sort(array);

        Move result = array[0];
        float max = -SearchDefinite(result, depth - 1, -0, 1);
        float alpha = max;

        Console.WriteLine($"{Board.ToPos(result.move)} : {max}");

        if (0 < max)
            return result.move;

        for (int i = 1; i < array.Length; i++)
        {
            Move move = array[i];
            float eval = -SearchDefinite(move, depth - 1, -alpha - 1, -alpha);

            if (0 < eval)
                return move.move;

            if (alpha < eval)
            {
                alpha = eval;
                eval = -SearchDefinite(move, depth - 1, -0, -alpha);

                if (0 < eval)
                    return move.move;

                alpha = Math.Max(alpha, eval);

                Console.WriteLine($"{Board.ToPos(move.move)} : {eval}");
            }
            else
            {
                Console.WriteLine($"{Board.ToPos(move.move)} : Rejected");
            }

            if (max < eval)
            {
                max = eval;
                result = move;
            }
        }
        return result.move;
    }

    public static float NegascoutDefinite(Move[] moves, int depth, float alpha, float beta)
    {
        float max = -SearchDefinite(moves[0], depth - 1, -beta, -alpha);

        if (beta <= max || 0 < max)
            return max;

        alpha = Math.Max(alpha, max);

        for (int i = 0; i < moves.Length; i++)
        {
            Move move = moves[i];
            float eval = -SearchDefinite(move, depth - 1, -alpha - 1, -alpha);

            if (beta <= eval)
                return eval;

            max = Math.Max(max, eval);
        }
        return max;
    }

    public static float NegamaxDefinite(Move[] moves, int depth, float alpha, float beta)
    {
        for (int i = 0; i < moves.Length; i++)
        {
            alpha = Math.Max(alpha, -SearchDefinite(moves[i], depth - 1, -beta, -alpha));

            if (alpha >= beta)
                return alpha;
        }
        return alpha;
    }

    public static float SearchDefinite(Move move, int depth, float alpha, float beta)
    {
        if (move.moves == 0)
        {
            ulong opponentMoves = move.reversed.GetOpponentMoves();
            if (opponentMoves == 0)
            {
                return EvalFinishedGame(move.reversed);
            }
            else
            {
                return -Search(new Move(move.reversed.ColorFliped(), 0, opponentMoves, Board.BitCount(opponentMoves)), depth - 1, -beta, -alpha);
            }
        }

        if (move.count == 1)
        {
            if (move.reversed.stoneCount == 62)
            {
                return EvalLastMove(move.reversed.Reversed(move.moves));
            }
            else if (move.reversed.stoneCount == 63)
            {
                return -EvalLastMove(move.reversed);
            }
            else
            {
                return -SearchDefinite(new Move(move.reversed, move.moves), depth - 1, -beta, -alpha);
            }
        }

        if (move.count == 2)
        {
            ulong next1 = Board.NextMove(move.moves);
            ulong next2 = Board.RemoveMove(move.moves, next1);

            alpha = Math.Max(alpha, -SearchDefinite(new Move(move.reversed, next1), depth - 1, -beta, -alpha));

            if (alpha >= beta)
                return alpha;

            return Math.Max(alpha, -SearchDefinite(new Move(move.reversed, next2), depth - 1, -beta, -alpha));
        }

        Move[] array = CreateMoveArray(move.reversed, move.moves, move.count);

        if (depth >= 3)
        {
            Array.Sort(array);
            return Negascout(array, depth, alpha, beta);
        }
        else
        {
            for (int i = 0; i < array.Length; i++)
            {
                alpha = Math.Max(alpha, -SearchDefinite(array[i], depth - 1, -beta, -alpha));

                if (alpha >= beta)
                    return alpha;
            }
            return alpha;
        }
    }

    public static ulong NegascoutRoot(Board board, int depth)
    {
        Move root = new Move(board);

        if (root.count <= 1)
        {
            return root.moves;
        }

        Move[] array = CreateMoveArray(root.reversed, root.moves, root.count);

        Array.Sort(array);

        Move result = array[0];
        float max = -Search(array[0], depth - 1, -1000000, 1000000);
        float alpha = max;

        Console.WriteLine($"{Board.ToPos(result.move)} : {max}");

        for (int i = 1; i < array.Length; i++)
        {
            Move move = array[i];

            float eval = -Search(move, depth - 1, -alpha - 1, -alpha);

            if (alpha < eval)
            {
                alpha = eval;
                eval = -Search(move, depth - 1, -1000000, -alpha);
                alpha = Math.Max(alpha, eval);

                Console.WriteLine($"{Board.ToPos(move.move)} : {eval}");
            }
            else
            {
                Console.WriteLine($"{Board.ToPos(move.move)} : Rejected");
            }

            if (max < eval)
            {
                max = eval;
                result = move;
            }
        }
        return result.move;
    }

    public static float Negascout(Move[] moves, int depth, float alpha, float beta)
    {
        float max = -Search(moves[0], depth - 1, -beta, -alpha);

        if (beta <= max)
            return max;

        alpha = Math.Max(alpha, max);

        for (int i = 1; i < moves.Length; i++)
        {
            Move move = moves[i];
            float eval = -Search(move, depth - 1, -alpha - 1, -alpha);

            if (beta <= eval)
                return eval;

            if (alpha < eval)
            {
                alpha = eval;
                eval = -Search(move, depth - 1, -beta, -alpha);

                if (beta <= eval)
                    return eval;

                alpha = Math.Max(alpha, eval);
            }
            max = Math.Max(max, eval);
        }
        return max;
    }

    public static float Negamax(Move[] moves, int depth, float alpha, float beta)
    {
        for (int i = 0; i < moves.Length; i++)
        {
            alpha = Math.Max(alpha, -Search(moves[i], depth - 1, -beta, -alpha));

            if (alpha >= beta)
                return alpha;
        }
        return alpha;
    }

    public static float Search(Move move, int depth, float alpha, float beta)
    {
        if (depth <= 0)
        {
            return Eval(move.reversed);
        }

        if (move.moves == 0)
        {
            ulong opponentMoves = move.reversed.GetOpponentMoves();
            if (opponentMoves == 0)
            {
                return EvalFinishedGame(move.reversed);
            }
            else
            {
                return -Search(new Move(move.reversed.ColorFliped(), 0, opponentMoves, Board.BitCount(opponentMoves)), depth - 1, -beta, -alpha);
            }
        }

        if (move.count == 1)
        {
            if (move.reversed.stoneCount == 63)
            {
                // return -EvalFinishedGame(move.reversed.Reversed(move.moves));
                return -EvalLastMove(move.reversed);
            }
            else
            {
                return -Search(new Move(move.reversed, move.moves), depth - 1, -beta, -alpha);
            }
        }

        if (move.count == 2)
        {
            ulong next1 = Board.NextMove(move.moves);
            ulong next2 = Board.RemoveMove(move.moves, next1);

            alpha = Math.Max(alpha, -Search(new Move(move.reversed, next1), depth - 1, -beta, -alpha));

            if (alpha >= beta)
                return alpha;

            return Math.Max(alpha, -Search(new Move(move.reversed, next2), depth - 1, -beta, -alpha));
        }

        Move[] array = CreateMoveArray(move.reversed, move.moves, move.count);

        if (move.count > 3 && depth >= 3)
        {
            Array.Sort(array);
            return Negascout(array, depth, alpha, beta);
        }
        else
        {
            return Negamax(array, depth, alpha, beta);
        }
    }

    public static Move[] CreateMoveArray(Board board, ulong moves, int count)
    {
        Move[] array = new Move[count];
        for (int i = 0; i < array.Length; i++)
        {
            ulong move = Board.NextMove(moves);
            moves = Board.RemoveMove(moves, move);
            array[i] = new Move(board, move);
        }
        return array;
    }

    public static readonly int[] POW3_TABLE = { 1, 3, 9, 27, 81, 243, 729, 2187, 6561, 19683, 59049 };
}

public readonly struct Move : IComparable<Move>
{
    public readonly ulong move;
    public readonly Board reversed;
    public readonly ulong moves;
    public readonly int count;

    public Move(Board board, ulong move)
    {
        this.move = move;
        reversed = board.Reversed(move);
        moves = reversed.GetMoves();
        count = Board.BitCount(moves);
    }

    public Move(Board reversed)
    {
        move = 0;
        this.reversed = reversed;
        moves = reversed.GetMoves();
        count = Board.BitCount(moves);
    }

    public Move(Board reversed, ulong move, ulong moves, int count)
    {
        this.move = move;
        this.reversed = reversed;
        this.moves = moves;
        this.count = count;
    }

    public int CompareTo([System.Diagnostics.CodeAnalysis.AllowNull] Move other)
    {
        return count - other.count;
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

    public static void CreateRotatedBoards(Board org, out Board tr, out Board hor, out Board rot90, out Board rot270)
    {
        tr = org.Transposed();
        hor = org.HorizontalMirrored();
        rot90 = hor.Transposed();
        rot270 = tr.HorizontalMirrored();
    }

    public Board HorizontalMirrored() => new Board(HorizontalMirror(bitB), HorizontalMirror(bitW), stoneCount);

    public static ulong HorizontalMirror(ulong x)
    {
        return BinaryPrimitives.ReverseEndianness(x);
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

    public static (int, int) ToPos(ulong move)
    {
        int x = BitOperations.TrailingZeroCount(move);
        return (x / 8, x & 7);
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

    public Board ColorFliped()
    {
        return new Board(bitW, bitB, stoneCount);
    }

    public int GetReversedCountOnLastMove()
    {
        int count = ReverseUtil.CountOnLastMove(bitB, bitW);
        if (count > 0)
        {
            return count * 2 + 1;
        }
        return ReverseUtil.CountOnLastMove(bitW, bitB) * -2 - 1;
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
                reversed = ReverseAvx(move, bitB, bitW);
                return new Board(bitB ^ (move | reversed), bitW ^ reversed, stoneCount + 1);

            case WHITE:
                reversed = ReverseAvx(move, bitW, bitB);
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
        return BitCount(bitB) - BitCount(bitW);
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

    public static ulong[,] MASK_TABLE ={
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

        p &= MASK_TABLE[pos, 3];

        int count = REVERSED_TABLE[y, (byte)(p >> (pos & 0x38))];
        count += REVERSED_TABLE[x, Bmi2.X64.ParallelBitExtract(p, MASK_TABLE[pos, 0])];
        count += REVERSED_TABLE[x, Bmi2.X64.ParallelBitExtract(p, MASK_TABLE[pos, 1])];
        count += REVERSED_TABLE[x, Bmi2.X64.ParallelBitExtract(p, MASK_TABLE[pos, 2])];

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
}

class Evaluation
{
    public static readonly byte[] array1 = {
        128, 203, 52, 84, 153, 101, 170, 154, 102, 138, 180, 96, 114, 216, 97, 145, 202, 112, 116, 159, 74, 109, 142, 52, 140, 157, 38, 81, 189, 101, 118, 129, 105, 175, 144, 115, 105, 143, 105, 72, 202, 74, 135, 160, 113, 61, 140, 98, 121, 130, 128, 132, 139, 95, 173, 153, 65, 79, 139, 110, 136, 149, 125, 194, 156, 114, 122, 159, 115, 133, 128, 125, 149, 150, 111, 119, 141, 94, 182, 180, 52, 81, 151, 97, 124, 133, 119, 132, 132, 121, 67, 135, 112, 124, 134, 114, 130, 130, 120, 98, 162, 98, 120, 135, 100, 144, 145, 97, 76, 142, 119, 124, 128, 123, 142, 133, 123, 170, 192, 77, 139, 224, 70, 143, 203, 101, 83, 131, 119, 123, 128, 128, 128, 134, 112, 128, 137, 111, 124, 129, 125, 131, 130, 124, 202, 140, 120, 125, 146, 116, 128, 128, 128, 153, 141, 109, 117, 134, 121, 140, 158, 89, 173, 157, 103, 123, 134, 122, 130, 136, 121, 156, 156, 92, 110, 157, 110, 134, 154, 120, 187, 142, 120, 124, 135, 124, 130, 140, 120, 128, 143, 118, 123, 130, 124, 130, 130, 125, 101, 145, 114, 114, 165, 96, 137, 133, 120, 52, 134, 114, 128, 128, 128, 129, 138, 109, 178, 135, 112, 113, 131, 122, 131, 132, 127, 171, 135, 124, 126, 142, 121, 131, 128, 127, 84, 177, 62, 111, 153, 52, 115, 184, 30, 138, 170, 104, 105, 136, 116, 155, 130, 122, 160, 146, 103, 126, 151, 107, 135, 141, 112, 128, 135, 117, 105, 133, 120, 130, 132, 102, 67, 136, 118, 125, 128, 122, 133, 129, 122, 97, 123, 117, 95, 155, 95, 136, 125, 117, 93, 136, 97, 126, 127, 128, 132, 130, 106, 156, 140, 109, 103, 138, 118, 134, 130, 126, 203, 133, 125, 128, 135, 120, 132, 128, 127, 151, 158, 119, 125, 135, 108, 181, 147, 61, 105, 146, 113, 123, 130, 124, 129, 129, 124, 97, 128, 121, 125, 128, 121, 130, 128, 122, 103, 160, 117, 121, 134, 105, 123, 137, 94, 170, 169, 92, 119, 131, 114, 163, 139, 104, 172, 175, 60, 190, 217, 56, 181, 157, 78, 122, 150, 99, 120, 131, 128, 137, 134, 93, 101, 129, 119, 121, 130, 125, 134, 130, 126, 206, 132, 128, 123, 138, 154, 128, 128, 128, 128, 147, 115, 119, 131, 139, 168, 128, 83, 194, 134, 123, 123, 131, 125, 129, 128, 124, 203, 136, 115, 128, 135, 112, 129, 132, 118, 161, 153, 122, 125, 136, 123, 158, 135, 114, 202, 153, 121, 127, 128, 124, 158, 129, 125, 206, 126, 129, 63, 162, 152, 145, 128, 129, 128, 128, 128, 128, 128, 128, 128, 128, 128, 171, 131, 120, 129, 130, 122, 132, 130, 128, 193, 122, 122, 125, 137, 149, 129, 128, 127, 132, 148, 85, 120, 133, 134, 148, 129, 49, 116, 150, 84, 99, 132, 124, 149, 138, 118, 128, 137, 119, 124, 153, 122, 149, 134, 122, 94, 151, 108, 119, 142, 113, 128, 148, 103, 98, 146, 115, 120, 128, 124, 151, 137, 116, 103, 136, 97, 73, 193, 108, 129, 146, 120, 51, 130, 121, 122, 128, 128, 126, 134, 120, 187, 137, 119, 121, 133, 125, 129, 133, 127, 161, 157, 118, 122, 148, 125, 128, 128, 127, 157, 137, 131, 118, 137, 129, 159, 159, 99, 61, 131, 121, 125, 130, 126, 131, 129, 124, 93, 132, 101, 96, 141, 119, 129, 132, 119, 51, 139, 119, 125, 136, 122, 126, 143, 120, 83, 135, 123, 122, 127, 124, 125, 133, 124, 122, 169, 107, 106, 205, 126, 134, 120, 121, 61, 132, 133, 125, 127, 128, 130, 106, 118, 52, 133, 101, 96, 129, 125, 128, 130, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 48, 125, 128, 110, 125, 128, 191, 102, 92, 149, 141, 109, 125, 130, 126, 131, 131, 124, 151, 138, 95, 132, 160, 117, 133, 149, 121, 157, 134, 126, 124, 132, 126, 129, 133, 126, 153, 135, 125, 121, 128, 125, 133, 129, 124, 128, 139, 107, 86, 172, 126, 136, 116, 124, 48, 126, 123, 128, 128, 128, 131, 100, 116, 84, 162, 85, 91, 150, 116, 136, 140, 124, 132, 155, 104, 117, 161, 120, 134, 128, 123, 83, 195, 79, 73, 176, 98, 64, 198, 37, 84, 127, 125, 126, 127, 127, 128, 127, 127, 105, 126, 127, 127, 129, 126, 127, 127, 127, 99, 128, 125, 127, 127, 127, 129, 127, 125, 124, 129, 127, 126, 127, 127, 129, 128, 127, 123, 126, 126, 132, 131, 126, 128, 127, 126, 125, 127, 127, 127, 127, 128, 127, 127, 127, 123, 127, 127, 126, 128, 127, 127, 127, 127, 123, 128, 127, 127, 128, 127, 127, 128, 127, 125, 127, 127, 127, 128, 126, 148, 130, 124, 118, 128, 126, 126, 127, 127, 127, 127, 127, 125, 127, 127, 127, 128, 127, 127, 127, 127, 120, 127, 127, 127, 127, 127, 129, 128, 127, 124, 128, 127, 128, 128, 127, 128, 127, 127, 119, 127, 126, 124, 138, 125, 128, 127, 127, 122, 127, 127, 127, 127, 128, 129, 127, 127, 123, 127, 127, 127, 127, 127, 128, 128, 127, 127, 127, 127, 127, 127, 128, 128, 128, 128, 121, 127, 126, 127, 127, 127, 170, 127, 125, 79, 127, 127, 126, 127, 127, 125, 127, 127, 103, 125, 119, 124, 134, 126, 127, 127, 126, 121, 128, 126, 126, 128, 127, 127, 128, 126, 124, 128, 127, 127, 127, 127, 128, 127, 127, 121, 127, 126, 126, 129, 127, 127, 127, 123, 96, 127, 126, 128, 128, 128, 128, 127, 125, 113, 127, 126, 115, 128, 127, 126, 128, 127, 129, 128, 127, 126, 129, 126, 127, 128, 127, 91, 137, 120, 124, 129, 118, 143, 130, 95, 114, 128, 127, 127, 127, 127, 126, 127, 127, 126, 125, 125, 127, 129, 126, 128, 125, 126, 124, 127, 127, 126, 127, 127, 128, 128, 126, 124, 128, 127, 127, 128, 127, 128, 128, 127, 125, 127, 127, 131, 132, 126, 127, 127, 127, 96, 127, 126, 127, 128, 128, 128, 125, 127, 110, 127, 126, 124, 130, 127, 126, 128, 127, 128, 127, 127, 127, 128, 127, 127, 128, 127, 132, 140, 126, 127, 131, 127, 142, 137, 115, 72, 130, 112, 132, 130, 124, 106, 129, 124, 95, 125, 121, 131, 130, 123, 132, 122, 121, 73, 155, 107, 120, 135, 116, 112, 145, 92, 139, 154, 104, 124, 129, 122, 111, 142, 93, 190, 131, 64, 220, 212, 49, 177, 62, 74, 106, 119, 106, 123, 127, 128, 133, 113, 101, 114, 117, 121, 126, 128, 124, 84, 127, 124, 63, 125, 121, 121, 127, 122, 128, 128, 128, 86, 118, 107, 120, 125, 123, 128, 101, 90, 122, 127, 127, 127, 127, 127, 127, 127, 127, 128, 126, 126, 127, 128, 126, 127, 127, 126, 122, 127, 127, 127, 128, 127, 127, 128, 127, 125, 127, 127, 127, 128, 127, 127, 127, 127, 123, 127, 127, 121, 126, 127, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 126, 126, 127, 126, 128, 127, 125, 127, 127, 125, 127, 127, 127, 127, 125, 127, 128, 127, 117, 127, 124, 126, 127, 125, 122, 115, 100, 109, 127, 126, 127, 128, 127, 128, 127, 127, 105, 127, 126, 126, 127, 127, 128, 127, 126, 119, 127, 127, 127, 127, 126, 127, 127, 126, 120, 127, 127, 127, 127, 127, 127, 128, 127, 121, 127, 125, 120, 128, 125, 127, 127, 126, 125, 127, 127, 127, 127, 128, 127, 127, 127, 124, 127, 127, 126, 128, 127, 127, 127, 127, 125, 127, 127, 127, 128, 127, 127, 128, 127, 124, 127, 127, 127, 127, 127, 123, 128, 124, 121, 127, 127, 127, 128, 127, 127, 127, 127, 126, 127, 127, 127, 127, 127, 127, 127, 128, 122, 127, 127, 127, 127, 127, 128, 127, 127, 123, 127, 127, 127, 128, 127, 127, 127, 128, 120, 127, 127, 123, 132, 127, 127, 128, 127, 125, 127, 127, 128, 128, 128, 128, 127, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 119, 127, 126, 127, 127, 127, 127, 127, 127, 125, 125, 124, 127, 129, 125, 128, 125, 126, 118, 127, 127, 127, 128, 127, 127, 128, 127, 117, 127, 127, 127, 128, 127, 128, 127, 127, 119, 127, 126, 120, 127, 126, 128, 127, 125, 110, 127, 127, 128, 128, 128, 127, 127, 127, 111, 126, 125, 124, 127, 127, 127, 127, 127, 120, 127, 126, 126, 126, 128, 127, 128, 127, 73, 127, 111, 126, 127, 122, 77, 112, 72, 170, 130, 127, 128, 128, 127, 128, 128, 127, 155, 129, 127, 126, 129, 127, 127, 128, 127, 149, 128, 128, 128, 128, 128, 128, 129, 125, 132, 128, 127, 127, 128, 127, 129, 128, 127, 129, 128, 127, 106, 130, 125, 127, 128, 127, 131, 128, 127, 127, 127, 128, 127, 128, 126, 130, 127, 125, 125, 128, 127, 128, 128, 127, 129, 128, 127, 127, 127, 127, 128, 128, 127, 131, 129, 128, 127, 128, 128, 123, 129, 123, 175, 128, 127, 129, 127, 128, 129, 128, 128, 133, 128, 126, 128, 128, 126, 128, 127, 127, 151, 136, 129, 127, 129, 128, 130, 129, 120, 142, 128, 127, 128, 128, 127, 139, 128, 126, 163, 134, 117, 111, 159, 124, 130, 137, 126, 125, 127, 127, 127, 128, 128, 129, 129, 126, 130, 127, 127, 128, 128, 127, 127, 127, 127, 158, 128, 127, 127, 129, 128, 128, 128, 128, 133, 129, 127, 128, 132, 128, 129, 128, 126, 136, 128, 127, 127, 128, 128, 128, 127, 127, 134, 128, 127, 126, 127, 127, 127, 127, 127, 129, 127, 127, 127, 127, 128, 128, 127, 127, 131, 128, 127, 128, 128, 127, 127, 127, 128, 134, 128, 127, 84, 129, 128, 128, 128, 128, 128, 127, 127, 128, 128, 128, 127, 127, 127, 131, 128, 127, 126, 128, 127, 128, 128, 127, 132, 128, 127, 125, 128, 128, 128, 128, 128, 136, 128, 127, 127, 128, 127, 131, 129, 117, 145, 129, 127, 127, 128, 127, 127, 127, 127, 135, 128, 128, 128, 128, 128, 128, 128, 127, 149, 128, 128, 128, 128, 127, 128, 128, 127, 130, 128, 128, 127, 128, 127, 128, 128, 127, 130, 127, 128, 132, 131, 126, 128, 128, 127, 129, 128, 128, 127, 128, 128, 128, 127, 127, 134, 128, 127, 127, 128, 127, 127, 128, 127, 129, 128, 128, 127, 128, 127, 128, 128, 128, 133, 130, 128, 128, 129, 128, 135, 130, 127, 135, 128, 128, 128, 128, 128, 127, 127, 127, 136, 128, 127, 127, 128, 127, 128, 128, 127, 129, 130, 129, 127, 129, 129, 127, 129, 126, 143, 130, 128, 128, 128, 127, 130, 128, 127, 181, 143, 128, 177, 182, 142, 128, 132, 127, 134, 128, 128, 127, 128, 128, 128, 127, 128, 137, 128, 127, 127, 127, 127, 128, 128, 127, 145, 128, 128, 127, 128, 128, 128, 128, 128, 136, 128, 128, 128, 130, 128, 134, 129, 128, 133, 128, 127, 127, 128, 128, 128, 127, 127, 132, 127, 128, 127, 127, 128, 128, 127, 127, 128, 128, 127, 127, 127, 128, 128, 127, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 131, 128, 128, 127, 127, 127, 128, 128, 127, 129, 128, 128, 127, 127, 128, 128, 128, 128, 134, 128, 128, 127, 127, 128, 131, 127, 122, 140, 128, 127, 129, 127, 127, 128, 128, 127, 130, 127, 127, 128, 128, 126, 128, 127, 127, 128, 129, 129, 127, 128, 129, 128, 128, 125, 144, 128, 127, 129, 127, 126, 130, 128, 124, 123, 128, 115, 112, 140, 118, 127, 128, 124, 126, 128, 127, 128, 127, 128, 128, 128, 126, 130, 128, 127, 127, 128, 127, 128, 127, 127, 158, 129, 128, 127, 128, 130,
     128, 128, 127, 129, 128, 127, 127, 128, 128, 124, 128, 123, 132, 128, 127, 127, 127, 127, 127, 127, 127, 132, 128, 127, 128, 128, 127, 128, 127, 127, 126, 128, 129, 127, 128, 128, 128, 129, 127, 128, 128, 129, 129, 128, 127, 129, 128, 126, 137, 130, 128, 133, 154, 140, 128, 129, 127, 130, 128, 128, 128, 128, 128, 128, 129, 127, 129, 128, 127, 128, 127, 127, 127, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 131, 128, 128, 127, 127, 128, 133, 127, 128, 182, 142, 125, 148, 130, 125, 123, 131, 125, 181, 147, 100, 142, 163, 110, 135, 138, 120, 159, 133, 129, 123, 134, 133, 124, 132, 125, 140, 134, 137, 170, 131, 127, 129, 130, 127, 168, 148, 137, 128, 164, 154, 134, 132, 129, 191, 134, 130, 128, 128, 128, 133, 132, 128, 115, 150, 100, 143, 162, 112, 131, 132, 125, 148, 148, 135, 122, 153, 141, 131, 128, 128, 64, 191, 123, 77, 180, 192, 34, 205, 43, 203, 139, 128, 127, 129, 128, 130, 129, 128, 170, 131, 128, 128, 130, 127, 129, 130, 127, 150, 129, 128, 127, 129, 127, 128, 129, 125, 151, 130, 128, 128, 127, 128, 128, 127, 127, 146, 129, 128, 130, 131, 126, 128, 128, 127, 131, 128, 128, 127, 127, 128, 128, 128, 127, 157, 129, 125, 127, 128, 127, 128, 127, 127, 134, 128, 127, 127, 128, 128, 128, 128, 127, 141, 128, 127, 127, 128, 127, 142, 129, 127, 189, 130, 129, 129, 128, 127, 128, 128, 127, 136, 127, 128, 128, 128, 127, 128, 127, 127, 146, 130, 128, 127, 128, 128, 128, 128, 128, 142, 128, 128, 128, 128, 127, 128, 127, 127, 169, 130, 126, 154, 146, 125, 130, 133, 126, 135, 128, 127, 127, 128, 128, 128, 127, 127, 143, 128, 128, 128, 128, 127, 128, 128, 127, 153, 128, 128, 127, 127, 128, 128, 128, 128, 135, 128, 127, 127, 127, 128, 134, 129, 127, 153, 129, 127, 127, 127, 127, 127, 128, 127, 140, 129, 127, 127, 130, 127, 128, 129, 127, 137, 128, 128, 127, 128, 128, 128, 128, 127, 137, 128, 128, 127, 128, 127, 127, 127, 128, 129, 128, 127, 117, 131, 127, 128, 128, 128, 133, 127, 127, 128, 128, 128, 128, 127, 127, 135, 128, 127, 127, 128, 127, 128, 128, 127, 131, 128, 127, 126, 128, 127, 128, 128, 127, 162, 129, 128, 126, 128, 127, 150, 130, 103, 180, 131, 127, 126, 127, 127, 129, 127, 127, 146, 127, 127, 125, 128, 127, 128, 128, 127, 137, 128, 128, 127, 127, 127, 127, 128, 126, 135, 127, 127, 127, 128, 127, 128, 128, 127, 128, 127, 127, 125, 128, 127, 128, 127, 127, 132, 128, 127, 127, 127, 128, 128, 127, 127, 156, 129, 127, 125, 128, 127, 128, 127, 128, 136, 127, 127, 126, 128, 127, 127, 128, 127, 138, 129, 128, 125, 128, 127, 147, 128, 124, 143, 129, 127, 126, 128, 127, 128, 127, 127, 123, 127, 127, 127, 127, 127, 127, 127, 127, 136, 130, 127, 127, 128, 127, 128, 128, 126, 192, 130, 126, 127, 128, 127, 134, 129, 127, 175, 129, 124, 131, 185, 115, 143, 117, 123, 169, 131, 127, 127, 127, 128, 130, 124, 125, 145, 128, 128, 127, 128, 127, 128, 128, 127, 126, 126, 126, 127, 127, 127, 128, 128, 128, 139, 129, 128, 127, 127, 127, 148, 123, 122, 156, 128, 127, 128, 127, 127, 128, 127, 127, 133, 127, 127, 127, 128, 127, 128, 127, 127, 157, 130, 128, 127, 128, 127, 129, 128, 127, 140, 128, 127, 127, 127, 127, 128, 127, 127, 132, 126, 126, 125, 127, 128, 128, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 135, 128, 127, 128, 128, 127, 128, 128, 128, 122, 126, 126, 127, 127, 126, 128, 128, 127, 155, 128, 128, 127, 127, 127, 148, 119, 116, 159, 129, 127, 128, 127, 128, 128, 128, 127, 135, 128, 128, 127, 128, 127, 128, 128, 127, 151, 129, 128, 127, 128, 127, 129, 128, 126, 162, 130, 128, 127, 128, 127, 136, 128, 127, 160, 130, 128, 155, 140, 127, 130, 129, 127, 139, 128, 127, 127, 127, 128, 128, 128, 127, 142, 128, 127, 128, 128, 128, 127, 127, 128, 153, 130, 127, 127, 129, 128, 128, 128, 127, 134, 128, 128, 127, 128, 128, 133, 132, 126, 140, 128, 128, 127, 127, 127, 128, 128, 127, 136, 128, 127, 127, 128, 127, 128, 127, 127, 130, 128, 128, 127, 128, 127, 128, 128, 127, 131, 128, 128, 127, 127, 127, 127, 127, 127, 150, 131, 127, 119, 145, 131, 128, 128, 127, 132, 127, 129, 127, 127, 128, 128, 128, 127, 134, 127, 127, 127, 127, 128, 127, 127, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 126, 127, 128, 127, 127, 128, 134, 126, 126, 150, 128, 127, 127, 128, 127, 129, 128, 127, 158, 129, 127, 140, 132, 127, 130, 131, 127, 137, 128, 128, 127, 128, 128, 128, 128, 127, 141, 128, 128, 127, 128, 127, 129, 128, 127, 147, 129, 128, 118, 135, 129, 128, 128, 128, 125, 127, 128, 128, 128, 128, 128, 127, 128, 177, 129, 128, 137, 129, 127, 128, 128, 127, 148, 128, 127, 127, 128, 128, 128, 128, 127, 195, 137, 131, 127, 132, 128, 191, 142, 76, 153, 129, 127, 127, 128, 127, 128, 128, 128, 136, 127, 127, 127, 128, 127, 128, 127, 127, 132, 127, 128, 128, 128, 127, 127, 127, 127, 133, 128, 128, 127, 128, 128, 127, 128, 128, 130, 128, 127, 130, 129, 128, 128, 128, 127, 130, 127, 127, 128, 128, 128, 127, 128, 127, 134, 127, 127, 127, 127, 127, 128, 127, 127, 131, 127, 128, 127, 128, 128, 128, 128, 128, 130, 128, 127, 127, 127, 127, 130, 128, 127, 129, 127, 127, 127, 128, 127, 128, 128, 128, 128, 128, 127, 128, 128, 127, 128, 128, 127, 128, 128, 127, 127, 128, 127, 127, 128, 127, 128, 128, 127, 128, 128, 128, 128, 128, 128, 131, 128, 127, 129, 132, 127, 128, 127, 127, 127, 127, 127, 128, 128, 128, 128, 127, 127, 130, 128, 128, 127, 128, 128, 128, 127, 128, 128, 127, 127, 128, 128, 127, 128, 128, 128, 128, 128, 127, 128, 127, 128, 131, 127, 127, 139, 128, 127, 128, 127, 128, 128, 128, 127, 138, 128, 128, 130, 129, 127, 128, 128, 127, 133, 128, 127, 128, 127, 127, 128, 128, 128, 129, 128, 127, 127, 128, 128, 128, 127, 128, 130, 128, 127, 128, 128, 127, 127, 128, 127, 129, 127, 127, 128, 128, 128, 127, 128, 128, 131, 128, 127, 128, 127, 128, 128, 127, 128, 130, 128, 127, 128, 128, 128, 127, 128, 128, 150, 129, 128, 127, 128, 127, 162, 130, 126, 216, 130, 129, 129, 128, 128, 129, 128, 127, 151, 128, 128, 129, 129, 127, 128, 130, 128, 153, 128, 128, 127, 128, 128, 128, 128, 128, 134, 128, 128, 128, 128, 128, 128, 128, 127, 128, 127, 127, 130, 131, 127, 128, 127, 127, 141, 128, 128, 127, 128, 128, 128, 127, 127, 157, 130, 127, 134, 129, 127, 127, 128, 128, 135, 128, 127, 128, 129, 127, 127, 128, 127, 160, 132, 129, 129, 130, 128, 163, 136, 127, 202, 131, 128, 131, 129, 128, 130, 128, 127, 155, 128, 129, 132, 131, 127, 131, 127, 127, 193, 140, 130, 128, 132, 128, 140, 139, 127, 224, 146, 151, 138, 132, 128, 159, 138, 129, 217, 185, 178, 212, 200, 183, 182, 181, 155, 205, 145, 139, 132, 132, 128, 154, 150, 139, 165, 131, 127, 129, 128, 127, 129, 129, 127, 162, 127, 128, 126, 132, 130, 128, 128, 128, 172, 135, 132, 127, 130, 128, 164, 129, 128, 159, 128, 127, 128, 128, 127, 127, 128, 127, 135, 128, 127, 128, 129, 128, 128, 127, 127, 148, 129, 128, 128, 128, 128, 128, 129, 128, 146, 127, 128, 127, 128, 128, 129, 127, 127, 138, 127, 127, 127, 132, 128, 128, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 142, 128, 127, 129, 128, 127, 128, 128, 128, 137, 127, 127, 127, 127, 129, 127, 128, 127, 161, 128, 129, 126, 127, 127, 153, 122, 116, 142, 129, 128, 127, 128, 128, 128, 128, 127, 133, 127, 127, 127, 128, 127, 128, 128, 128, 142, 128, 127, 127, 128, 127, 128, 128, 127, 135, 128, 127, 127, 128, 128, 129, 128, 127, 134, 128, 128, 135, 132, 127, 129, 127, 127, 136, 128, 128, 127, 127, 128, 128, 127, 127, 135, 128, 128, 128, 127, 128, 127, 128, 127, 136, 128, 127, 128, 128, 128, 127, 128, 128, 132, 128, 128, 128, 128, 128, 134, 130, 128, 130, 127, 127, 127, 128, 128, 127, 127, 127, 127, 127, 128, 128, 128, 128, 128, 128, 128, 128, 127, 128, 127, 127, 127, 127, 127, 127, 128, 128, 128, 127, 128, 128, 128, 127, 128, 131, 127, 127, 127, 132, 128, 128, 128, 128, 127, 127, 127, 128, 128, 128, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 141, 128, 127, 128, 127, 127, 128, 127, 127, 135, 128, 128, 131, 130, 127, 129, 128, 127, 137, 128, 128, 127, 128, 128, 128, 129, 128, 134, 127, 127, 127, 127, 127, 132, 128, 127, 131, 127, 127, 125, 130, 128, 130, 127, 128, 125, 127, 127, 128, 128, 128, 127, 128, 128, 153, 128, 128, 129, 128, 127, 128, 128, 128, 133, 127, 127, 127, 127, 128, 127, 128, 127, 176, 132, 132, 127, 127, 127, 180, 123, 100, 154, 129, 127, 127, 128, 128, 128, 126, 127, 130, 127, 127, 127, 128, 127, 127, 128, 128, 138, 128, 127, 127, 128, 127, 128, 128, 127, 132, 128, 127, 127, 128, 127, 128, 127, 128, 129, 127, 127, 129, 128, 127, 127, 127, 128, 129, 128, 127, 127, 127, 128, 127, 127, 128, 136, 128, 128, 127, 128, 127, 127, 127, 127, 128, 127, 127, 127, 128, 127, 127, 128, 128, 131, 128, 127, 127, 127, 127, 131, 128, 127, 144, 127, 127, 128, 128, 128, 128, 127, 128, 129, 128, 127, 128, 128, 127, 128, 128, 127, 137, 128, 127, 128, 128, 127, 128, 128, 127, 133, 127, 127, 127, 128, 128, 128, 127, 127, 139, 129, 127, 142, 138, 128, 128, 127, 127, 133, 127, 127, 127, 127, 128, 128, 127, 127, 130, 127, 127, 127, 127, 128, 127, 128, 128, 129, 127, 127, 127, 127, 127, 128, 128, 128, 129, 128, 128, 127, 128, 127, 130, 127, 128, 149, 127, 127, 127, 127, 128, 128, 127, 127, 130, 127, 128, 128, 128, 128, 128, 127, 128, 133, 127, 127, 127, 128, 128, 127, 127, 127, 130, 128, 128, 128, 127, 128, 127, 128, 128, 130, 128, 128, 127, 129, 128, 128, 127, 127, 130, 127, 127, 128, 128, 128, 128, 128, 127, 132, 128, 127, 128, 127, 128, 128, 128, 127, 130, 128, 127, 127, 128, 128, 128, 128, 128, 140, 128, 127, 127, 128, 128, 132, 130, 127, 202, 130, 127, 127, 127, 127, 128, 128, 127, 141, 128, 127, 125, 130, 127, 128, 129, 127, 134, 128, 127, 127, 128, 128, 127, 127, 126, 130, 127, 127, 127, 128, 127, 127, 128, 127, 128, 127, 127, 122, 127, 127, 128, 127, 127, 132, 127, 127, 127, 128, 128, 127, 127, 127, 154, 129, 127, 127, 128, 127, 127, 127, 127, 132, 127, 127, 127, 127, 127, 127, 128, 127, 149, 131, 128, 125, 128, 127, 138, 128, 127, 160, 128, 128, 127, 128, 128, 128, 127, 127, 125, 127, 127, 127, 127, 127, 128, 127, 127, 146, 129, 127, 127, 127, 128, 128, 128, 127, 203, 133, 127, 127, 127, 127, 137, 127, 127, 157, 117, 127, 62, 181, 132, 132, 128, 128, 120, 128, 128, 128, 128, 128, 129, 127, 127, 133, 128, 127, 127, 128, 127, 128, 127, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 116, 128, 128, 127, 127, 128, 132, 127, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 157, 129, 128, 127, 127, 127, 129, 128, 127, 132, 128, 127, 128, 128, 128, 128, 127, 128, 148, 128, 128, 127, 128, 127, 128, 127, 127, 145, 128, 128, 128, 128, 127, 129, 128, 127, 137, 128, 128, 145, 139, 128, 129, 128, 128, 143, 128, 128, 127, 127, 128, 129, 127, 127, 140, 128, 127, 128, 128, 127, 127, 127, 128, 135, 128, 127, 128, 129, 128, 127, 128, 128, 133, 128, 127, 128, 129, 128, 132, 130, 128, 139, 128, 127, 127, 128, 127, 128, 127, 128, 130, 127, 126, 125, 127, 127, 127, 127, 127, 134, 128, 127, 127, 127, 127, 128, 127, 127, 134, 127, 128, 127, 127, 127, 129, 127, 127, 134, 124, 127, 113, 150, 132, 127, 127, 127, 106, 128, 128, 127, 127, 128, 129, 124, 126, 138, 127, 127, 127, 128, 127, 127, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 100, 127, 127, 127, 128, 128, 132, 124, 124, 180, 129, 128, 130, 128, 128, 129, 128, 127, 147, 128, 127, 137, 136, 127, 130, 128, 127, 159, 132, 128, 128, 130, 127, 128, 130, 128, 158, 129, 127, 127, 127, 127, 128, 127, 127, 128, 123, 126, 101, 129, 128, 129, 127, 127, 102, 126, 126, 128, 128, 128, 127, 124, 126, 184, 130, 129, 130, 130, 127, 129, 130, 128, 129, 119, 123, 115, 122, 123, 127, 128, 127, 198, 142, 139, 112, 123, 123, 205, 82, 71, 52, 128, 116, 125, 127, 125, 127, 127, 126, 104, 127, 125, 127, 129, 126, 127, 127, 126, 84, 127, 124, 126, 128, 125, 127, 128, 124, 97, 129, 126, 126, 127, 128, 127, 127, 127, 113, 127, 126, 112, 128, 125, 128, 128, 127, 121, 128, 126, 127, 127, 128, 127, 127, 126, 103, 127, 125, 127, 127, 127, 127, 127, 127, 123, 127, 127, 127, 127, 127, 127, 128, 127, 109, 127, 126, 126, 127, 126, 125, 128, 123, 101, 128, 126, 127, 128, 127, 127, 127, 127, 118, 127, 126, 127, 128, 127, 128, 127, 127, 115, 128, 126, 127, 127, 126, 127, 128, 125, 119, 128, 127, 127, 127, 127, 127, 127, 127, 92, 126, 126, 104, 151, 125, 128, 127, 127, 123, 128, 127, 127, 128, 128, 129, 128, 126, 118, 128, 127, 127, 127, 127, 127, 128, 127, 121, 127, 127, 127, 128, 128, 128, 128, 128, 125, 128, 126, 127, 127, 126, 137, 127, 123, 65, 125, 125, 127, 127, 127, 125, 128, 127, 109, 127, 125, 126, 127, 127, 127, 127, 127, 119, 127, 127, 127, 128, 127, 127, 127, 127, 111, 128, 127, 127, 128, 127, 127, 127, 127, 119, 128, 127, 121, 127, 125, 127, 127, 127, 101, 127, 127, 128, 128, 128, 127, 127, 127, 112, 127, 126, 126, 127, 127, 127, 127, 127, 120, 127, 127, 127, 127, 127, 128, 128, 127, 85, 128, 125, 125, 128, 121, 100, 129, 108, 96, 128, 125, 127, 127, 127, 127, 127, 127, 103, 127, 125, 125, 128, 126, 128, 127, 126, 119, 128, 127, 126, 127, 127, 127, 127, 126, 112, 128, 126, 127, 127, 127, 126, 127, 127, 121, 127, 126, 121, 129, 122, 127, 127, 126, 101, 127, 124, 127, 128, 128, 127, 126, 125, 92, 127, 125, 119, 128, 127, 127, 128, 127, 115, 127, 127, 126, 127, 127, 128, 128, 127, 95, 127, 125, 124, 128, 125, 100, 127, 115, 105, 128, 126, 126, 127, 127, 127, 127, 127, 117, 127, 126, 127, 127, 127, 128, 127, 127, 97, 128, 126, 125, 128, 124, 115, 128, 122, 77, 126, 126, 126, 127, 126, 117, 127, 125, 60, 124, 117, 64, 178, 112, 128, 127, 123, 107, 127, 126, 127, 127, 128, 128, 127, 126, 114, 127, 127, 126, 127, 127, 127, 128, 127, 129, 126, 127, 127, 127, 128, 128, 128, 128, 107, 128, 126, 126, 127, 126, 137, 126, 119, 114, 127, 127, 127, 128, 127, 127, 127, 127, 125, 127, 127, 127, 127, 127, 128, 127, 127, 118, 127, 127, 127, 127, 127, 128, 127, 127, 120, 128, 127, 127, 127, 127, 127, 127, 127, 128, 126, 127, 121, 128, 129, 128, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 124, 127, 127, 127, 127, 127, 127, 127, 127, 122, 126, 127, 127, 127, 126, 128, 128, 127, 104, 127, 124, 126, 127, 127, 135, 123, 109, 74, 128, 124, 125, 128, 127, 128, 127, 127, 117, 128, 127, 127, 128, 127, 128, 127, 127, 108, 128, 127, 127, 127, 127, 129, 128, 126, 98, 128, 126, 127, 127, 127, 129, 127, 127, 117, 127, 126, 107, 130, 126, 129, 127, 127, 119, 128, 127, 127, 128, 128, 129, 127, 127, 120, 128, 127, 126, 127, 127, 127, 127, 128, 122, 128, 127, 127, 128, 128, 127, 128, 128, 126, 128, 127, 127, 128, 127, 129, 128, 127, 98, 128, 126, 127, 127, 127, 127, 127, 127, 97, 127, 124, 126, 128, 126, 128, 127, 127, 121, 127, 127, 127, 128, 128, 127, 128, 127, 119, 127, 127, 127, 127, 127, 127, 127, 127, 99, 127, 126, 106, 139, 135, 128, 128, 127, 133, 129, 128, 127, 127, 128, 128, 128, 128, 114, 127, 127, 126, 127, 127, 127, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 123, 128, 128, 127, 127, 128, 130, 126, 127, 111, 127, 126, 127, 127, 127, 128, 127, 127, 119, 128, 125, 126, 129, 127, 128, 128, 127, 131, 128, 127, 127, 128, 127, 127, 127, 128, 109, 127, 126, 126, 127, 127, 127, 128, 127, 115, 128, 126, 107, 132, 132, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 127, 128, 62, 128, 125, 120, 128, 126, 127, 127, 127, 85, 128, 124, 124, 129, 130, 128, 128, 127, 79, 131, 125, 111, 132, 137, 123, 139, 70, 101, 128, 125, 127, 127, 128, 127, 128, 127, 116, 127, 127, 127, 128, 126, 127, 127, 127, 124, 128, 127, 127, 128, 127, 127, 127, 127, 119, 127, 127, 127, 127, 128, 128, 128, 127, 124, 127, 127, 124, 128, 127, 128, 128, 127, 126, 127, 127, 127, 128, 128, 127, 127, 127, 122, 127, 127, 127, 128, 127, 128, 128, 127, 125, 127, 127, 127, 127, 127, 128, 128, 128, 126, 127, 127, 127, 127, 127, 125, 128, 127, 105, 128, 128, 127, 128, 128, 127, 127, 128, 122, 127, 127, 127, 128, 127, 127, 127, 127, 124, 127, 127, 127, 128, 127, 126, 127, 126, 123, 127, 127, 127, 128, 127, 127, 128, 127, 114, 127, 126, 122, 128, 125, 127, 127, 126, 124, 127, 127, 127, 128, 128, 127, 127, 126, 124, 127, 127, 127, 128, 127, 127, 128, 127, 124, 127, 127, 127, 128, 127, 128, 128, 128, 125, 127, 127, 127, 127, 127, 127, 127, 126, 110, 127, 127, 127, 127, 127, 127, 127, 127, 118, 127, 127, 127, 127, 127, 127, 127, 127, 125, 128, 127, 127, 128, 127, 127, 127, 127, 125, 127, 127, 127, 128, 127, 127, 128, 127, 125, 127, 127, 124, 127, 127, 127, 127, 127, 125, 128, 127, 128, 128, 128, 127, 127, 127, 122, 127, 127, 127, 128, 127, 127, 128, 128, 122, 127, 127, 127, 127, 127, 127, 128, 127, 116, 127, 126, 127, 127, 128, 112, 127, 116, 97, 127, 126, 126, 127, 126, 127, 127, 127, 107, 127, 126, 126, 127, 128, 128, 127, 127, 122, 127, 127, 127, 127, 127, 126, 128, 127, 114, 127, 127, 127, 127, 127, 126, 127, 127, 121, 127, 127, 123, 127, 125, 127, 127, 126, 119, 127, 126, 127, 128, 128, 127, 127, 125, 110, 127, 127, 126, 127, 127, 127, 128, 127, 112, 127, 127, 126, 128, 127, 128, 128, 127, 117, 127, 127, 125, 127, 127, 110, 127, 116, 74, 126, 125, 126, 128, 127, 125, 127, 127, 95, 127, 122, 126, 127, 125, 126, 127, 125, 108, 127, 126, 125, 127, 127, 118, 128, 118, 70, 125, 125, 125, 127, 125, 124, 128, 124, 56, 115, 112, 49, 183, 172, 142, 132, 131, 126, 131, 135, 127, 128, 128, 140, 132, 132, 96, 127, 125, 127, 127, 127, 128, 128, 128, 152, 128, 129, 127, 128, 130, 128, 128, 128, 126, 129, 132, 126, 128, 127, 154, 128, 125, 115, 128, 127, 127, 128, 127, 127, 127, 127, 120, 127, 127, 127, 127, 127, 127, 127, 127, 125, 128, 128, 127, 128, 127, 130, 128, 128, 116, 128, 128, 128, 127, 127, 128, 127, 127, 154, 127, 128, 122, 130, 130, 128, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 121, 127, 127, 126, 128, 127, 128, 128, 127, 149, 126, 126, 125, 129, 130, 128, 128, 127, 120, 128, 130, 128, 128, 128, 141, 123, 104, 52, 127, 125, 127, 127, 127, 128, 127, 127, 120, 127, 127, 127, 128, 127, 127, 128, 127, 113, 127, 127, 126, 127, 126, 129, 127, 124, 100, 128, 126, 127, 127, 127, 128, 127, 126, 105, 127, 124, 116, 128, 127, 129, 128, 127, 122, 127, 128, 127, 127, 128, 128, 127, 127, 124, 128, 127, 127, 127, 127, 128, 128, 128, 123, 127, 127, 127, 128, 127, 128, 128, 128, 126, 128, 127, 127, 128, 127, 133, 127, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 94, 127, 126, 126, 127, 127, 128, 127, 127, 108, 127, 125, 127, 128, 127, 128, 127, 127, 129, 128, 127, 127, 128, 127, 128, 128, 127, 121, 128, 126, 127, 128, 127, 128, 127, 127, 139, 127, 126, 123, 128, 127, 128, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 52, 127, 121, 118, 127, 128, 127, 128, 127, 134, 127, 127, 125, 127, 128, 128, 128, 128, 98, 128, 137, 122, 127, 128, 192, 123, 74, 102, 128, 126, 127, 128, 127, 127, 127, 127, 122, 127, 127, 127, 127, 127, 127, 127, 127, 118, 127, 127, 127, 127, 127, 127, 127, 127, 121, 127, 127, 127, 128, 128, 128, 128, 127, 124, 127, 127, 124, 127, 127, 127, 127, 127, 124, 127, 127, 127, 127, 128, 127, 128, 127, 121, 127, 127, 127, 127, 127, 127, 127, 128, 124, 127, 127, 127, 127, 127, 127, 128, 128, 124, 127, 127, 127, 127, 127, 125, 127, 126, 115, 127, 127, 127, 128, 127, 127, 128, 127, 122, 127, 127, 127, 127, 127, 127, 127, 127, 116, 127, 127, 127, 127, 126, 124, 127, 125, 123, 127, 127, 127, 128, 127, 126, 127, 127, 104, 127, 125, 93, 129, 124, 127, 127, 126, 124, 127, 127, 128, 128, 128, 126, 127, 127, 125, 128, 127, 127, 128, 127, 128, 128, 128, 125, 127, 127, 127, 127, 127, 128, 128, 128, 124, 127, 127, 127, 127, 127, 127, 127, 126, 125, 127, 127, 127, 127, 127, 127, 127, 128, 126, 128, 127, 127, 128, 127, 127, 127, 127, 127, 128, 128, 127, 127, 128, 127, 128, 127, 124, 127, 127, 127, 128, 127, 127, 128, 128, 126, 127, 127, 124, 127, 128, 127, 127, 128, 127, 127, 128, 128, 128, 128, 127, 127, 127, 127, 127, 127, 127, 128, 128, 127, 127, 128, 128, 128, 127, 127, 128, 127, 127, 128, 128, 124, 127, 127, 127, 128, 127, 125, 128, 123, 112, 127, 126, 127, 127, 127, 127, 128, 127, 112, 127, 126, 126, 128, 127, 127, 127, 127, 122, 127, 127, 126, 128, 127, 127, 128, 127, 120, 127, 127, 127, 127, 127, 127, 127, 127, 122, 127, 127, 121, 127, 125, 127, 127, 126, 119, 127, 127, 128, 128, 128, 127, 127, 127, 120, 127, 127, 126, 127, 127, 127, 128, 127, 118, 127, 127, 126, 127, 127, 127, 128, 127, 121, 127, 127, 126, 127, 127, 120, 127, 123, 113, 127, 127, 126, 127, 127, 127, 128, 127, 117, 127, 126, 127, 127, 126, 127, 127, 126, 120, 127, 127, 126, 127, 127, 124, 128, 124, 101, 126, 127, 127, 127, 126, 126, 127, 126, 78, 123, 123, 74, 155, 131, 127, 128, 128, 121, 127, 127, 127, 128, 128, 127, 127, 128, 120, 128, 127, 123, 127, 127, 128, 127, 128, 129, 127, 127, 128, 127, 127, 128, 128, 128, 124, 128, 128, 125, 128, 127, 129, 127, 124, 125, 127, 127, 127, 128, 128, 127, 128, 128, 127, 127, 127, 127, 127, 127, 128, 127, 127, 127, 127, 128, 127, 128, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 127, 127, 127, 127, 128, 127, 128, 128, 128, 127, 127, 127, 127, 127, 127, 128, 128, 128, 123, 127, 127, 127, 127, 128, 128, 127, 123, 38, 125, 124, 125, 127, 127, 125, 127, 127, 102, 126, 126, 126, 128, 127, 127, 126, 127, 103, 126, 126, 126, 127, 124, 125, 127, 125, 97, 128, 125, 127, 127, 126, 120, 127, 125, 94, 126, 122, 92, 127, 118, 126, 127, 124, 120, 127, 127, 127, 127, 128, 127, 127, 126, 120, 127, 127, 126, 128, 127, 127, 127, 127, 114, 127, 127, 127, 128, 128, 127, 128, 128, 126, 127, 128, 127, 128, 127, 125, 128, 124, 95, 127, 126, 127, 127, 127, 126, 128, 127, 106, 127, 125, 127, 127, 125, 127, 127, 127, 120, 127, 127, 127, 127, 127, 126, 127, 126, 112, 127, 126, 127, 127, 126, 126, 127, 127, 93, 125, 126, 101, 139, 132, 128, 127, 128, 118, 127, 128, 127, 128, 128, 127, 126, 128, 109, 127, 127, 125, 128, 127, 127, 127, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 116, 128, 128, 127, 128, 128, 128, 126, 123, 52, 127, 123, 124, 127, 127, 123, 127, 126, 61, 124, 115, 115, 127, 116, 127, 127, 123, 99, 126, 127, 124, 128, 127, 123, 128, 124, 89, 127, 123, 125, 127, 126, 126, 128, 126, 83, 122, 119, 90, 128, 125, 128, 127, 124, 92, 126, 127, 128, 128, 128, 128, 124, 123, 30, 103, 108, 95, 126, 116, 117, 127, 123, 49, 116, 109, 100, 116, 104, 122, 128, 123, 37, 76, 70, 72, 100, 74, 43, 71, 54,};

    public static readonly byte[] array2 = {
        128, 128, 128, 95, 129, 128, 159, 128, 125, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 107, 128, 128, 75, 131, 128, 135, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 133, 128, 128, 124, 129, 128, 121, 128, 127, 131, 128, 128, 125, 128, 128, 127, 128, 125, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 116, 128, 128, 128, 128, 123, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 127, 147, 128, 128, 119, 127, 128, 179, 128, 123, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 132, 128, 138, 128, 127, 128, 128, 128, 128, 127, 128, 127, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 121, 128, 128, 133, 127, 128, 130, 128, 125, 128, 128, 128, 128, 128, 128, 128, 128, 128, 123, 128, 128, 127, 129, 128, 129, 128, 127, 94, 128, 128, 127, 127, 128, 126, 128, 127, 97, 129, 128, 126, 128, 128, 128, 127, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 90, 128, 128, 119, 128, 128, 128, 128, 127, 120, 129, 128, 127, 128, 128, 127, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 125, 128, 128, 127, 127, 128, 127, 128, 127, 125, 127, 128, 127, 129, 128, 127, 128, 127, 127, 128, 128, 127, 128, 128, 128, 128, 127, 128, 128, 128, 126, 128, 128, 128, 128, 127, 132, 127, 128, 127, 128, 128, 129, 128, 127, 128, 128, 128, 127, 128, 128, 128, 128, 128, 101, 128, 128, 126, 128, 128, 128, 128, 127, 120, 128, 128, 127, 128, 128, 129, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 129, 128, 128, 129, 128, 128, 128, 128, 128, 127, 127, 128, 128, 133, 128, 127, 127, 127, 126, 128, 128, 127, 128, 128, 128, 128, 128, 122, 128, 128, 127, 128, 128, 127, 128, 128, 127, 130, 128, 127, 127, 128, 129, 127, 127, 128, 128, 128, 128, 127, 128, 128, 128, 127, 160, 128, 128, 128, 127, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 158, 128, 126, 128, 127, 127, 129, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 153, 128, 128, 126, 128, 128, 128, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 134, 128, 127, 126, 128, 127, 128, 128, 127, 132, 128, 128, 127, 127, 128, 127, 128, 128, 127, 128, 128, 127, 128, 128, 127, 128, 127, 127, 128, 125, 126, 127, 128, 127, 128, 127, 125, 128, 128, 126, 127, 128, 125, 128, 127, 128, 128, 128, 127, 128, 128, 128, 128, 128, 127, 128, 127, 127, 127, 128, 128, 128, 121, 164, 128, 128, 126, 127, 128, 135, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 134, 128, 125, 127, 127, 127, 127, 128, 127, 128, 128, 128, 128, 128, 128, 129, 128, 128, 128, 128, 128, 128, 128, 128, 127, 128, 128, 122, 128, 128, 126, 128, 128, 127, 128, 127, 129, 128, 128, 128, 127, 128, 128, 128, 127, 127, 128, 128, 128, 128, 128, 128, 128, 128, 129, 128, 127, 127, 128, 127, 127, 128, 126, 70, 128, 128, 119, 128, 128, 131, 128, 127, 91, 128, 128, 128, 128, 128, 128, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 94, 128, 128, 126, 127, 128, 127, 128, 127, 128, 128, 128, 127, 128, 128, 128, 128, 128, 127, 128, 128, 128, 128, 128, 127, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 129, 128, 128, 128, 128, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 75, 128, 128, 96, 128, 128, 131, 128, 127, 119, 128, 128, 124, 127, 128, 127, 128, 127, 131, 128, 128, 128, 128, 128, 127, 128, 127, 95, 128, 128, 110, 128, 128, 118, 128, 127, 117, 128, 128, 123, 127, 128, 127, 128, 123, 120, 128, 128, 125, 128, 128, 128, 128, 127, 123, 128, 128, 127, 127, 128, 128, 128, 127, 127, 128, 128, 127, 127, 128, 127, 128, 127, 130, 128, 128, 127, 128, 128, 127, 128, 127, 87, 128, 128, 127, 129, 128, 148, 128, 127, 102, 128, 128, 126, 128, 128, 126, 128, 127, 128, 128, 128, 128, 127, 128, 128, 128, 128, 119, 128, 128, 124, 128, 128, 127, 128, 128, 122, 128, 128, 124, 128, 128, 127, 128, 126, 128, 128, 128, 128, 128, 128, 127, 128, 127, 123, 128, 128, 127, 129, 128, 131, 128, 126, 126, 128, 128, 127, 127, 128, 128, 128, 127, 130, 128, 128, 126, 129, 128, 132, 128, 125, 179, 128, 128, 128, 128, 128, 131, 128, 127, 105, 132, 128, 127, 127, 128, 126, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 177, 128, 128, 131, 127, 128, 127, 128, 127, 124, 129, 128, 125, 129, 128, 126, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 124, 127, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 116, 128, 128, 122, 128, 128, 129, 128, 127, 100, 129, 128, 123, 128, 128, 128, 128, 127, 127, 128, 128, 128, 128, 128, 127, 128, 128, 127, 128, 128, 127, 127, 128, 128, 128, 127, 114, 129, 128, 122, 135, 128, 126, 128, 127, 128, 128, 128, 128, 127, 128, 128, 128, 127, 127, 128, 128, 128, 127, 128, 130, 128, 127, 128, 127, 128, 127, 128, 128, 129, 128, 128, 127, 128, 128, 127, 127, 128, 127, 128, 128, 139, 128, 128, 127, 127, 128, 135, 128, 127, 123, 127, 128, 127, 128, 128, 129, 128, 127, 128, 128, 128, 128, 128, 128, 127, 128, 128, 155, 128, 128, 128, 128, 128, 132, 128, 128, 126, 129, 128, 127, 133, 128, 126, 127, 127, 128, 128, 128, 127, 127, 128, 128, 128, 127, 124, 128, 128, 128, 127, 128, 131, 128, 128, 126, 128, 128, 127, 129, 128, 129, 129, 127, 129, 128, 128, 128, 127, 128, 128, 128, 127, 104, 128, 128, 127, 127, 128, 127, 128, 127, 119, 128, 128, 127, 128, 128, 127, 128, 127, 137, 128, 127, 127, 128, 127, 128, 128, 127, 120, 128, 128, 126, 128, 128, 127, 128, 125, 128, 128, 128, 127, 128, 128, 128, 128, 128, 120, 128, 127, 127, 127, 125, 127, 128, 127, 125, 128, 128, 133, 127, 128, 127, 128, 127, 116, 128, 128, 127, 128, 128, 126, 128, 128, 125, 128, 122, 129, 128, 127, 128, 128, 121, 86, 128, 128, 126, 127, 128, 129, 128, 127, 125, 128, 128, 127, 128, 128, 127, 128, 127, 130, 128, 126, 127, 128, 127, 127, 128, 127, 125, 128, 128, 117, 128, 128, 127, 128, 121, 124, 128, 128, 123, 128, 128, 126, 128, 127, 126, 128, 120, 127, 127, 122, 127, 128, 123, 121, 128, 128, 126, 128, 128, 126, 128, 126, 126, 128, 128, 127, 127, 128, 127, 128, 128, 126, 128, 126, 129, 127, 127, 128, 128, 116, 156, 128, 128, 127, 128, 128, 129, 128, 128, 125, 128, 128, 127, 128, 128, 127, 128, 127, 128, 128, 125, 128, 127, 127, 128, 128, 126, 117, 128, 128, 125, 127, 128, 127, 128, 126, 126, 128, 128, 126, 128, 128, 127, 128, 127, 126, 128, 126, 127, 128, 126, 127, 128, 126, 124, 128, 128, 127, 128, 128, 127, 128, 127, 126, 128, 128, 127, 127, 128, 127, 128, 127, 127, 128, 126, 128, 127, 127, 127, 128, 123, 184, 128, 128, 123, 127, 128, 135, 128, 126, 128, 128, 128, 128, 128, 128, 128, 128, 128, 163, 128, 128, 127, 128, 128, 127, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 125, 128, 128, 127, 128, 128, 128, 128, 127, 160, 128, 128, 127, 127, 128, 128, 128, 127, 127, 128, 128, 127, 128, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 127, 128, 128, 167, 128, 128, 106, 127, 128, 127, 128, 125, 128, 128, 128, 126, 128, 128, 128, 128, 127, 152, 128, 128, 129, 128, 128, 128, 128, 126, 131, 128, 128, 123, 129, 128, 127, 128, 126, 125, 128, 128, 122, 129, 128, 128, 128, 126, 129, 128, 128, 127, 127, 128, 127, 128, 127, 135, 128, 128, 127, 127, 128, 130, 128, 127, 126, 128, 128, 127, 127, 128, 126, 128, 127, 132, 128, 128, 128, 129, 128, 131, 128, 127, 179, 128, 128, 123, 128, 128, 158, 128, 126, 123, 128, 128, 127, 127, 128, 127, 128, 127, 135, 128, 128, 128, 127, 128, 131, 128, 127, 131, 128, 128, 127, 128, 128, 127, 128, 127, 124, 128, 128, 127, 128, 128, 127, 128, 127, 127, 128, 128, 127, 127, 128, 127, 128, 127, 159, 128, 128, 136, 127, 128, 144, 128, 126, 135, 128, 128, 126, 128, 128, 129, 128, 126, 138, 128, 128, 128, 131, 128, 131, 128, 127, 150, 128, 128, 127, 128, 128, 128, 128, 127, 117, 127, 128, 127, 128, 128, 127, 127, 127, 135, 128, 128, 128, 127, 128, 127, 128, 127, 129, 128, 128, 127, 127, 128, 121, 128, 128, 129, 132, 128, 128, 133, 128, 126, 128, 128, 139, 128, 128, 128, 127, 128, 128, 128, 128, 134, 128, 128, 127, 130, 128, 128, 128, 127, 134, 127, 128, 128, 128, 128, 128, 129, 127, 128, 128, 128, 128, 128, 128, 127, 128, 128, 98, 128, 128, 125, 128, 128, 127, 128, 127, 127, 130, 128, 127, 129, 128, 128, 127, 127, 129, 128, 128, 127, 128, 128, 127, 128, 127, 130, 128, 128, 127, 128, 128, 127, 128, 127, 127, 128, 128, 127, 131, 128, 127, 128, 127, 129, 128, 128, 128, 127, 128, 128, 128, 127, 138, 128, 128, 127, 128, 128, 129, 128, 127, 129, 129, 128, 127, 129, 128, 127, 129, 127, 129, 128, 128, 127, 127, 128, 129, 128, 128, 168, 128, 128, 126, 128, 128, 128, 128, 127, 124, 128, 128, 128, 128, 128, 127, 127, 127, 129, 128, 128, 127, 127, 128, 128, 128, 128, 134, 128, 128, 128, 128, 128, 128, 128, 127, 128, 129, 128, 127, 138, 128, 125, 127, 127, 129, 128, 128, 127, 127, 128, 128, 128, 127, 129, 128, 128, 127, 133, 128, 137, 128, 127, 128, 134, 128, 127, 131, 128, 128, 132, 127, 130, 128, 128, 128, 128, 128, 132, 128, 127, 75, 128, 128, 124, 127, 128, 126, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 149, 128, 122, 129, 128, 127, 128, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 130, 128, 128, 126, 127, 127, 127, 128, 127, 77, 128, 128, 127, 127, 128, 124, 128, 128, 127, 128, 128, 128, 127, 128, 128, 128, 127, 131, 128, 126, 129, 127, 127, 129, 128, 126, 115, 128, 128, 119, 127, 128, 127, 128, 127, 128, 128, 128, 127, 128, 128, 128, 128, 128, 131, 128, 127, 126, 127, 127, 127, 128, 127, 130, 128, 128, 123, 127, 128, 127, 128, 128, 126, 128, 128, 127, 128, 128, 127, 128, 127, 129, 128, 127, 125, 127, 126, 128, 128, 125, 99, 128, 128, 123, 127, 128, 126, 128, 127, 127, 128, 128, 127, 127, 128, 127, 128, 127, 128, 128, 126, 128, 127, 127, 128, 128, 122, 138, 128, 128, 126, 128, 128, 132, 128, 127, 127, 128, 128, 127, 128, 128, 127, 128, 127, 154, 128, 125, 126, 127, 127, 131, 128, 127, 127, 128, 128, 124, 128, 128, 128, 128, 127, 127, 128, 128, 127, 128, 128, 128, 128, 127, 127, 128, 127, 125, 127, 127, 128, 128, 127, 127, 128, 128, 126, 128, 128, 127, 128, 127, 127, 128, 128, 127, 127, 128, 127, 128, 127, 140, 128, 126, 128, 127, 126, 132, 128, 120, 121, 128, 128, 99, 131, 128, 164, 128, 126, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 93, 133, 128, 126, 128, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 132, 128, 127, 128, 127, 128, 128, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 133, 128, 128, 70, 145, 128, 194, 128, 132, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 81, 142, 128, 99, 170, 128, 129, 129, 124, 128, 128, 126, 127, 129, 127, 127, 128, 126, 128, 128, 128, 128, 131, 128, 128, 127, 128, 142, 128, 113, 141, 127, 127, 129, 128, 124, 128, 128, 127, 128, 128, 127, 128, 128, 127, 127, 127, 128, 129, 130, 128, 127, 128, 127, 129, 128, 128, 116, 153, 128, 173, 128, 125, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 111, 170, 128, 125, 153, 128, 119, 139, 127, 127, 128, 127, 127, 128, 128, 126, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 144, 128, 126, 136, 128, 127, 133, 128, 116, 128, 128, 127, 128, 128, 128, 128, 128, 127, 126, 134, 128, 127, 137, 128, 131, 129, 124, 95, 128, 128, 137, 127, 128, 132, 128, 127, 104, 131, 128, 126, 128, 128, 128, 127, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 118, 129, 127, 127, 128, 127, 127, 127, 127, 125, 128, 127, 127, 128, 128, 127, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 127, 128, 128, 128, 127, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 96, 128, 128, 129, 131, 128, 139, 128, 123, 98, 131, 128, 112, 131, 128, 128, 127, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 117, 130, 126, 122, 133, 127, 117, 127, 126, 120, 127, 127, 125, 132, 127, 127, 127, 126, 128, 127, 128, 127, 127, 128, 128, 128, 127, 129, 128, 127, 126, 128, 127, 127, 128, 127, 128, 127, 127, 128, 127, 127, 128, 128, 127, 127, 128, 128, 128, 128, 127, 128, 128, 128, 111, 128, 128, 129, 130, 128, 131, 128, 127, 111, 129, 128, 127, 128, 128, 127, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 122, 129, 127, 127, 130, 128, 124, 128, 127, 122, 129, 127, 127, 133, 128, 127, 128, 127, 127, 127, 128, 128, 127, 128, 128, 127, 128, 128, 128, 126, 127, 127, 127, 128, 128, 127, 128, 129, 127, 127, 128, 128, 128, 129, 127, 127, 130, 127, 127, 130, 127, 128, 128, 126, 138, 128, 128, 123, 128, 128, 123, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 164, 128, 126, 128, 127, 127, 129, 128, 127, 125, 129, 128, 127, 127, 128, 127, 127, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 127, 128, 127, 127, 127, 128, 128, 128, 127, 130, 128, 127, 128, 128, 127, 127, 127, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 127, 127, 128, 128, 128, 128, 128, 128, 123, 128, 128, 104, 129, 128, 124, 128, 126, 128, 128, 128, 128, 128, 128, 128, 128, 128, 148, 128, 125, 123, 128, 127, 132, 128, 126, 113, 128, 128, 121, 133, 128, 126, 128, 126, 128, 128, 127, 127, 128, 127, 127, 127, 127, 123, 128, 125, 125, 128, 127, 126, 127, 124, 130, 128, 126, 127, 130, 127, 130, 127, 127, 127, 128, 128, 127, 128, 128, 128, 128, 128, 128, 128, 126, 128, 127, 127, 127, 128, 119, 113, 128, 128, 122, 129, 128, 127, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 123, 128, 126, 128, 128, 128, 132, 128, 126, 120, 133, 128, 127, 131, 128, 124, 129, 127, 128, 128, 128, 128, 127, 128, 128, 128, 127, 126, 128, 126, 127, 128, 128, 127, 127, 126, 131, 132, 127, 129, 129, 127, 131, 129, 126, 128, 128, 128, 128, 128, 128, 128, 128, 127, 128, 128, 127, 128, 128, 127, 129, 127, 123, 126, 128, 128, 100, 129, 128, 140, 128, 128, 115, 128, 128, 127, 128, 128, 125, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 100, 139, 128, 124, 128, 127, 127, 128, 126, 124, 132, 128, 127, 127, 127, 127, 127, 127, 127, 128, 128, 127, 128, 127, 127, 128, 127, 138, 128, 122, 127, 127, 127, 128, 128, 128, 128, 128, 127, 127, 127, 128, 127, 127, 128, 128, 128, 128, 128, 128, 127, 128, 128, 128, 80, 128, 128, 71, 142, 128, 176, 128, 123, 97, 128, 128, 96, 129, 128, 126, 128, 122, 142, 128, 128, 126, 128, 128, 131, 128, 126, 101, 172, 128, 76, 170, 125, 114, 138, 127, 106, 134, 122, 113, 144, 125, 120, 140, 106, 92, 128, 128, 116, 134, 125, 129, 127, 123, 139, 128, 104, 129, 129, 125, 132, 131, 118, 128, 128, 126, 127, 127, 127, 128, 128, 125, 126, 127, 124, 128, 128, 127, 128, 128, 124, 94, 128, 128, 114, 142, 128, 145, 128, 126, 98, 128, 128, 124, 132, 128, 131, 128, 126, 127, 128, 128, 127, 127, 128, 132, 128, 127, 111, 153, 128, 126, 152, 128, 119, 163, 126, 122, 133, 127, 124, 132, 127, 116, 131, 119, 125, 128, 128, 126, 130, 127, 124, 127, 127, 147, 128, 122, 128, 128, 127, 145, 128, 116, 128, 128, 126, 128, 128, 128, 127, 130, 122, 128, 128, 127, 128, 129, 126, 128, 129, 118, 135, 128, 128, 128, 129, 128, 138, 128, 124, 68, 150, 128, 113, 130, 128, 125, 128, 128, 149, 128, 128, 128, 128, 128, 129, 128, 127, 84, 131, 126, 126, 127, 127, 125, 128, 127, 94, 174, 127, 126, 134, 127, 125, 131, 127, 127, 128, 127, 127, 127, 127, 127, 128, 126, 133, 128, 124, 127, 128, 127, 127, 127, 127, 139, 129, 127, 128, 127, 127, 130, 129, 127, 128, 128, 127, 128, 128, 127, 127, 128, 127, 145, 128, 128, 143, 152, 128, 171, 128, 118, 67, 167, 128, 89, 138, 128, 162, 127, 80, 157, 128, 128, 139, 127, 128, 132, 128, 122, 46, 151, 119, 81, 111, 107, 91, 124, 91, 63, 153, 109, 70, 200, 140, 64, 175, 123, 129, 128, 125, 130, 179, 124, 127, 127, 126, 131, 128, 105, 139, 128, 125, 126, 129, 116, 142, 129, 123, 134, 159, 128, 133, 138, 130, 132, 134, 125, 128, 137, 128, 129, 132, 125, 160, 128, 128, 126, 132, 128, 172, 128, 119, 77, 140, 128, 126, 144, 128, 151, 139, 124, 144, 128, 128, 128, 127, 128, 141, 128, 126, 51, 175, 127, 118, 120, 125, 79, 127, 116, 71, 194, 126, 113, 194, 129, 65, 182, 126, 128, 127, 127, 126, 162, 125, 127, 128, 126, 164, 128, 113, 146, 129, 126, 152, 128, 100, 180, 146, 125, 151, 143, 127, 188, 146, 115, 134, 145, 127, 131, 137, 119, 145, 133, 111, 88, 128, 128, 123, 127, 128, 125, 128, 127, 120, 128, 128, 127, 128, 128, 127, 128, 127, 182, 128, 127, 127, 127, 127, 131, 128, 126, 120, 131, 128, 126, 127, 127, 126, 127, 127, 125, 131, 128, 127, 128, 127, 127, 128, 127, 118, 129, 122, 127, 127, 125, 126, 127, 125, 130, 128, 120, 128, 128, 127, 127, 128, 126, 127, 129, 127, 128, 127, 128, 127, 129, 127, 131, 127, 124, 128, 128, 127, 127, 128, 119, 75, 128, 128, 57, 131, 128, 112, 128, 115, 108, 128, 128, 114, 128, 128, 122, 128, 125, 161, 128, 102, 145, 127, 73, 143, 128, 72, 69, 133, 128, 116, 128, 109, 96, 126, 87, 119, 127, 111, 112, 163, 135, 120, 168, 140, 84, 131, 69, 63, 163, 121, 81, 128, 118, 131, 127, 108, 129, 127, 125, 132, 130, 125, 127, 127, 126, 127, 128, 127, 128, 128, 129, 132, 129, 110, 132, 127, 129, 131, 128, 113, 100, 128, 128, 125, 130, 128, 130, 128, 126, 110, 128, 128, 127, 130, 128, 120, 128, 126, 114, 128, 109, 127, 128, 127, 142, 128, 100, 100, 140, 128, 125, 129, 127, 112, 129, 124, 121, 138, 127, 124, 151, 126, 120, 139, 124, 89, 130, 121, 113, 158, 129, 73, 130, 113, 138, 129, 121, 129, 127, 127, 133, 128, 122, 127, 130, 125, 127, 130, 127, 126, 146, 122, 134, 131, 122, 136, 116, 109, 139, 125, 79, 129, 128, 128, 121, 128, 128, 132, 128, 126, 128, 128, 128, 128, 128, 128, 128, 128, 128, 138, 128, 128, 128, 127, 128, 128, 128, 127, 108, 135, 128, 125, 128, 128, 127, 128, 127, 128, 128, 128, 128, 127, 128, 128, 128, 128, 123, 128, 128, 127, 128, 127, 127, 128, 127, 141, 128, 125, 128, 127, 128, 128, 128, 127, 126, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 125, 127, 128, 127, 128, 127, 127, 124, 128, 128, 80, 131, 128, 142, 128, 121, 126, 128, 128, 125, 127, 128, 127, 128, 127, 136, 128, 128, 127, 128, 128, 136, 128, 116, 93, 139, 128, 96, 139, 127, 126, 128, 123, 126, 130, 127, 123, 139, 127, 124, 136, 127, 124, 133, 128, 122, 142, 127, 126, 128, 126, 146, 128, 117, 144, 129, 120, 132, 130, 118, 130, 128, 127, 128, 130, 128, 128, 128, 125, 136, 128, 122, 135, 139, 126, 130, 130, 121, 143, 128, 128, 119, 132, 128, 170, 128, 122, 125, 128, 128, 126, 128, 128, 126, 128, 127, 132, 128, 128, 126, 128, 128, 148, 128, 126, 128, 148, 128, 121, 142, 126, 114, 143, 124, 128, 146, 127, 126, 132, 127, 123, 132, 127, 126, 137, 128, 127, 136, 127, 126, 128, 127, 135, 128, 118, 134, 129, 126, 151, 112, 98, 128, 128, 128, 128, 133, 128, 129, 142, 126, 121, 139, 126, 129, 168, 125, 139, 142, 116, 140, 128, 128, 126, 127, 128, 126, 128, 127, 114, 157, 128, 127, 128, 128, 128, 128, 128, 138, 128, 128, 128, 127, 128, 128, 128, 127, 123, 147, 127, 127, 128, 128, 127, 128, 127, 125, 158, 128, 127, 133, 128, 127, 128, 128, 128, 130, 127, 128, 128, 127, 128, 128, 127, 126, 128, 127, 127, 128, 128, 126, 132, 127, 139, 129, 127, 127, 128, 128, 127, 140, 128, 129, 128, 126, 128, 127, 128, 128, 127, 127, 148, 128, 128, 128, 129, 128, 125, 128, 118, 116, 150, 128, 127, 151, 128, 128, 128, 127, 133, 128, 128, 141, 134, 128, 131, 128, 122, 100, 161, 127, 114, 153, 127, 125, 129, 126, 117, 145, 127, 115, 194, 128, 117, 171, 127, 135, 148, 125, 131, 143, 125, 133, 128, 124, 138, 128, 126, 137, 134, 127, 129, 136, 127, 145, 130, 127, 151, 142, 127, 139, 125, 113, 132, 128, 121, 135, 132, 126, 131, 130, 123, 166, 128, 128, 136, 146, 128, 164, 128, 125, 123, 169, 128, 125, 139, 128, 147, 149, 126, 144, 128, 128, 128, 146, 128, 138, 128, 127, 80, 198, 127, 122, 127, 127, 114, 119, 126, 116, 152, 127, 125, 179, 127, 111, 151, 125, 128, 128, 127, 127, 125, 127, 127, 128, 127, 155, 128, 126, 141, 148, 128, 116, 198, 127, 159, 173, 126, 133, 126, 127, 174, 128, 111, 137, 200, 123, 132, 99, 119, 147, 109, 105, 85, 128, 128, 118, 128, 128, 119, 128, 127, 127, 128, 128, 127, 127, 128, 127, 128, 128, 155, 128, 125, 126, 128, 127, 130, 128, 126, 121, 150, 128, 125, 128, 127, 126, 128, 127, 127, 128, 128, 128, 127, 128, 128, 127, 128, 116, 138, 126, 126, 127, 127, 126, 128, 126, 135, 132, 126, 128, 128, 127, 128, 131, 127, 127, 128, 127, 128, 127, 128, 127, 127, 127, 135, 129, 118, 128, 128, 127, 132, 129, 119, 92, 128, 128, 78, 134, 128, 118, 128, 123, 123, 128, 128, 125, 128, 128, 127, 128, 127, 181, 128, 117, 133, 131, 122, 150, 128, 116, 61, 168, 128, 99, 178, 126, 92, 129, 126, 125, 126, 125, 123, 142, 127, 126, 142, 127, 60, 139, 115, 61, 169, 118, 71, 130, 115, 166, 129, 113, 154, 139, 130, 136, 138, 130, 127, 127, 127, 127, 128, 127, 128, 129, 116, 162, 128, 96, 184, 151, 108, 150, 127, 70, 141, 128, 128, 115, 146, 128, 159, 128, 125, 122, 128, 128, 127, 128, 128, 124, 128, 126, 214, 128, 118, 143, 129, 125, 206, 128, 119, 79, 197, 128, 126, 158, 127, 91, 147, 125, 125, 129, 126, 126, 132, 127, 127, 128, 122, 70, 145, 121, 106, 150, 120, 89, 136, 118, 172, 134, 120, 139, 158, 127, 129, 191, 132, 127, 130, 127, 127, 129, 127, 128, 132, 115, 139, 177, 107, 172, 134, 107, 181, 118, 69, 133, 128, 128, 91, 128, 128, 156, 128, 123, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 123, 127, 128, 127, 127, 128, 127, 127, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 161, 128, 122, 127, 127, 127, 129, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 125, 128, 128, 81, 129, 128, 138, 128, 102, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 110, 128, 128, 121, 139, 128, 118, 127, 127, 128, 128, 120, 124, 131, 126, 127, 128, 117, 128, 128, 128, 128, 128, 128, 128, 128, 128, 143, 128, 84, 135, 128, 115, 129, 128, 102, 128, 128, 127, 128, 128, 126, 128, 128, 127, 127, 127, 128, 129, 128, 128, 127, 128, 127, 121, 128, 128, 60, 122, 128, 184, 128, 110, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 112, 141, 128, 126, 130, 128, 113, 127, 127, 128, 128, 127, 127, 128, 128, 125, 128, 125, 128, 127, 128, 128, 127, 128, 128, 127, 128, 173, 128, 112, 125, 130, 126, 155, 128, 84, 128, 128, 127, 128, 128, 128, 128, 128, 124, 126, 128, 128, 128, 128, 128, 127, 127, 126, 116, 128, 128, 131, 128, 128, 131, 128, 127, 90, 129, 128, 125, 128, 128, 126, 127, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 125, 127, 127, 127, 127, 127, 127, 128, 127, 127, 127, 127, 128, 128, 128, 128, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 129, 128, 126, 127, 127, 127, 128, 128, 127, 127, 127, 127, 127, 127, 128, 127, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 141, 128, 128, 128, 128, 128, 132, 128, 126, 131, 129, 128, 123, 129, 128, 127, 127, 126, 128, 128, 128, 128, 128, 128, 128, 128, 128, 124, 128, 123, 124, 129, 126, 126, 128, 126, 126, 128, 126, 126, 131, 128, 127, 127, 126, 128, 127, 128, 127, 128, 128, 128, 128, 128, 134, 128, 122, 130, 127, 125, 128, 128, 123, 128, 128, 126, 128, 129, 127, 127, 128, 127, 128, 128, 128, 128, 127, 127, 128, 128, 128, 131, 128, 128, 130, 128, 128, 150, 128, 125, 106, 130, 128, 123, 129, 128, 131, 127, 126, 128, 128, 128, 128, 128, 128, 128, 128, 128, 124, 128, 127, 124, 128, 127, 127, 127, 124, 126, 129, 127, 127, 135, 128, 127, 128, 127, 128, 127, 128, 127, 128, 128, 128, 128, 128, 141, 128, 126, 129, 129, 127, 133, 128, 121, 131, 130, 127, 129, 130, 127, 129, 128, 127, 127, 127, 127, 128, 127, 127, 127, 127, 126, 159, 128, 128, 123, 127, 128, 117, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 150, 128, 123, 126, 127, 127, 128, 128, 127, 126, 127, 128, 128, 127, 128, 127, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 128, 126, 127, 127, 128, 128, 127, 128, 128, 128, 137, 128, 125, 127, 128, 127, 127, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 129, 128, 126, 127, 128, 127, 127, 128, 127, 143, 128, 128, 123, 128, 128, 125, 128, 124, 128, 128, 128, 128, 128, 128, 128, 128, 128, 143, 128, 126, 127, 128, 127, 128, 128, 126, 126, 128, 128, 127, 128, 128, 128, 127, 127, 127, 127, 125, 127, 129, 126, 128, 127, 124, 126, 127, 125, 127, 127, 126, 127, 128, 127, 132, 127, 125, 130, 128, 126, 128, 127, 125, 127, 128, 128, 128, 128, 127, 128, 128, 127, 132, 127, 125, 128, 128, 127, 128, 128, 121, 158, 128, 128, 115, 131, 128, 125, 128, 124, 128, 128, 128, 128, 128, 128, 128, 128, 128, 157, 128, 124, 127, 127, 127, 142, 128, 124, 126, 128, 128, 127, 127, 128, 128, 127, 127, 127, 128, 128, 128, 127, 128, 127, 127, 126, 127, 128, 127, 127, 127, 128, 127, 127, 127, 137, 128, 124, 137, 128, 127, 132, 127, 122, 127, 128, 127, 128, 128, 128, 127, 128, 127, 135, 127, 127, 128, 129, 127, 129, 127, 122, 125, 128, 128, 122, 128, 128, 133, 128, 127, 116, 128, 128, 126, 128, 128, 127, 128, 127, 128, 128, 128, 128, 128, 128, 128, 128, 128, 113, 129, 128, 127, 128, 126, 127, 127, 127, 127, 129, 128, 127, 128, 127, 128, 128, 127, 128, 127, 128, 127, 128, 126, 127, 128, 127, 146, 128, 119, 127, 128, 127, 130, 128, 126, 131, 128, 126, 127, 128, 127, 127, 127, 127, 128, 128, 127, 128, 128, 127, 128, 128, 127, 111, 128, 128, 84, 132, 128, 135, 128, 123, 122, 128, 128, 107, 129, 128, 128, 128, 126, 130, 128, 128, 128, 127, 128, 128, 128, 127, 119, 136, 128, 104, 156, 142, 121, 128, 126, 133, 129, 116, 115, 138, 112, 126, 129, 86, 126, 127, 128, 126, 129, 113, 127, 128, 121, 126, 128, 106, 140, 130, 111, 133, 128, 112, 129, 128, 117, 128, 128, 127, 128, 127, 118, 126, 127, 108, 131, 128, 123, 128, 127, 123, 130, 128, 128, 112, 133, 128, 174, 128, 124, 118, 128, 128, 119, 138, 128, 128, 128, 126, 128, 128, 128, 128, 127, 128, 129, 128, 127, 108, 137, 128, 122, 136, 124, 110, 134, 125, 119, 133, 127, 124, 133, 125, 119, 129, 115, 124, 128, 128, 126, 129, 127, 127, 128, 125, 162, 128, 115, 129, 131, 127, 158, 128, 116, 130, 128, 121, 128, 129, 127, 132, 128, 113, 128, 127, 125, 131, 128, 118, 131, 128, 116, 169, 128, 128, 135, 128, 128, 136, 128, 126, 99, 130, 128, 124, 128, 128, 128, 128, 127, 127, 128, 128, 127, 128, 128, 128, 128, 128, 119, 128, 123, 126, 127, 123, 127, 127, 126, 119, 136, 126, 123, 135, 126, 127, 128, 126, 127, 127, 127, 127, 127, 127, 127, 128, 127, 133, 128, 105, 128, 128, 127, 130, 128, 127, 138, 128, 117, 128, 128, 127, 129, 128, 127, 128, 128, 126, 128, 128, 127, 127, 128, 127, 113, 128, 128, 95, 130, 128, 140, 128, 108, 40, 137, 128, 48, 135, 128, 111, 130, 125, 133, 128, 128, 130, 128, 128, 128, 128, 127, 82, 135, 120, 125, 123, 64, 115, 128, 96, 115, 147, 77, 74, 186, 136, 83, 147, 121, 127, 127, 125, 126, 139, 123, 127, 128, 125, 175, 128, 57, 164, 129, 107, 128, 127, 97, 184, 133, 109, 165, 136, 118, 148, 135, 104, 130, 128, 125, 128, 132, 126, 128, 128, 123, 162, 128, 128, 136, 131, 128, 176, 128, 120, 73, 137, 128, 105, 138, 128, 121, 133, 124, 131, 128, 128, 128, 127, 128, 129, 128, 128, 88, 141, 125, 118, 124, 117, 101, 124, 115, 92, 159, 126, 104, 185, 127, 70, 146, 103, 127, 128, 127, 126, 139, 126, 127, 128, 126, 193, 128, 86, 162, 129, 125, 155, 128, 76, 194, 139, 115, 183, 139, 125, 193, 137, 85, 129, 129, 128, 129, 127, 113, 132, 127, 113, 114, 128, 128, 128, 127, 128, 128, 128, 127, 116, 128, 128, 127, 128, 128, 127, 128, 127, 140, 128, 97, 127, 127, 126, 128, 128, 126, 128, 128, 128, 128, 128, 122, 127, 128, 126, 126, 129, 128, 127, 128, 127, 127, 128, 127, 116, 127, 126, 128, 127, 114, 127, 128, 127, 131, 127, 107, 128, 128, 127, 127, 128, 126, 127, 128, 124, 127, 128, 127, 127, 127, 127, 130, 127, 97, 127, 128, 127, 128, 128, 121, 88, 128, 128, 91, 129, 128, 118, 128, 108, 110, 128, 128, 117, 128, 128, 126, 128, 108, 131, 128, 85, 107, 129, 105, 129, 128, 115, 99, 128, 128, 138, 128, 57, 113, 127, 106, 118, 131, 54, 107, 149, 145, 122, 135, 155, 95, 128, 81, 80, 143, 126, 122, 128, 128, 174, 127, 56, 141, 128, 135, 132, 127, 127, 127, 128, 127, 127, 128, 127, 127, 127, 130, 138, 128, 102, 144, 129, 103, 130, 127, 75, 106, 128, 128, 129, 136, 128, 126, 128, 125, 121, 128, 128, 123, 133, 128, 114, 128, 120, 138, 128, 104, 126, 128, 127, 128, 128, 103, 117, 128, 128, 126, 128, 118, 117, 128, 120, 122, 134, 126, 123, 132, 124, 119, 129, 123, 109, 128, 125, 116, 142, 129, 104, 127, 112, 155, 128, 93, 129, 128, 126, 140, 127, 101, 120, 130, 107, 121, 130, 126, 124, 129, 111, 137, 128, 109, 138, 127, 83, 139, 126, 60, 128, 128, 128, 114, 126, 128, 154, 128, 125, 128, 128, 128, 128, 128, 128, 128, 128, 128, 139, 128, 128, 129, 128, 128, 128, 128, 127, 116, 132, 128, 126, 127, 128, 127, 127, 128, 128, 127, 128, 128, 128, 128, 128, 128, 128, 126, 127, 128, 127, 128, 127, 128, 128, 127, 154, 128, 115, 128, 129, 127, 131, 127, 127, 127, 128, 127, 127, 127, 128, 127, 128, 128, 131, 128, 123, 128, 127, 127, 127, 127, 127, 160, 128, 128, 109, 128, 128, 140, 128, 112, 127, 128, 128, 122, 128, 128, 127, 128, 127, 156, 128, 128, 124, 128, 128, 131, 128, 123, 107, 132, 128, 110, 138, 126, 127, 128, 126, 126, 128, 127, 127, 136, 125, 127, 129, 126, 126, 128, 128, 127, 132, 124, 127, 128, 127, 143, 128, 101, 136, 128, 92, 129, 127, 102, 129, 128, 126, 130, 128, 127, 128, 128, 124, 132, 127, 121, 139, 135, 123, 131, 128, 122, 174, 128, 128, 78, 131, 128, 183, 128, 112, 113, 128, 128, 123, 128, 128, 128, 128, 127, 157, 128, 128, 129, 132, 128, 158, 128, 126, 115, 150, 128, 122, 137, 123, 125, 129, 126, 129, 130, 127, 126, 130, 127, 126, 128, 126, 126, 128, 128, 127, 129, 127, 127, 128, 127, 153, 128, 82, 140, 127, 116, 178, 129, 84, 162, 128, 127, 125, 132, 128, 139, 129, 121, 148, 133, 120, 134, 148, 114, 141, 130, 111, 166, 128, 128, 129, 128, 128, 131, 128, 127, 72, 127, 128, 124, 129, 128, 128, 127, 127, 135, 128, 128, 128, 128, 128, 127, 128, 127, 124, 134, 127, 127, 129, 127, 127, 127, 128, 124, 130, 127, 127, 135, 128, 127, 127, 128, 127, 127, 126, 128, 127, 126, 128, 128, 127, 134, 128, 124, 128, 128, 128, 129, 128, 127, 136, 132, 126, 129, 130, 127, 128, 129, 127, 129, 128, 123, 127, 127, 127, 128, 127, 127, 154, 128, 128, 124, 128, 128, 129, 128, 124, 140, 145, 128, 112, 154, 128, 128, 128, 127, 144, 128, 128, 135, 128, 128, 128, 128, 125, 116, 133, 125, 122, 132, 127, 126, 127, 127, 120, 132, 123, 116, 175, 129, 119, 145, 139, 127, 129, 124, 129, 132, 109, 128, 127, 125, 154, 128, 114, 142, 130, 125, 130, 128, 126, 165, 133, 124, 181, 141, 124, 141, 125, 96, 134, 128, 117, 134, 130, 115, 131, 128, 104, 179, 128, 128, 142, 139, 128, 197, 128, 124, 93, 152, 128, 111, 182, 128, 109, 181, 127, 146, 128, 128, 133, 130, 128, 140, 128, 126, 123, 146, 127, 123, 129, 125, 126, 129, 127, 123, 144, 126, 123, 142, 126, 123, 125, 127, 127, 128, 127, 127, 126, 127, 127, 128, 127, 185, 128, 121, 158, 168, 129, 138, 145, 126, 170, 186, 123, 173, 136, 127, 192, 134, 91, 136, 143, 127, 134, 115, 87, 143, 119, 91, 119, 128, 128, 116, 130, 128, 126, 128, 126, 105, 128, 128, 126, 128, 128, 126, 128, 128, 186, 128, 104, 129, 127, 126, 141, 128, 124, 121, 131, 128, 128, 128, 127, 127, 128, 127, 126, 128, 128, 127, 127, 128, 128, 127, 128, 115, 128, 126, 125, 128, 125, 127, 127, 127, 170, 128, 123, 129, 128, 127, 129, 128, 127, 127, 128, 127, 128, 128, 128, 128, 127, 127, 160, 127, 80, 129, 128, 124, 129, 127, 120, 94, 128, 128, 82, 135, 128, 128, 128, 122, 111, 128, 128, 113, 128, 128, 127, 128, 127, 177, 128, 114, 104, 131, 116, 128, 128, 110, 90, 141, 128, 102, 154, 126, 108, 128, 126, 120, 128, 109, 110, 143, 122, 123, 135, 117, 74, 129, 109, 66, 139, 108, 103, 128, 111, 203, 128, 79, 175, 138, 127, 137, 130, 134, 126, 127, 127, 127, 129, 126, 128, 129, 93, 184, 128, 61, 189, 128, 72, 141, 126, 60, 109, 128, 128, 83, 137, 128, 111, 128, 102, 97, 128, 128, 123, 132, 128, 116, 128, 128, 187, 128, 87, 92, 175, 127, 165, 128, 116, 123, 149, 128, 128, 138, 125, 115, 130, 127, 122, 129, 121, 125, 129, 122, 126, 127, 117, 112, 131, 125, 121, 125, 117, 120, 126, 96, 208, 135, 103, 164, 164, 131, 173, 147, 143, 126, 130, 126, 127, 128, 127, 125, 130, 75, 192, 145, 101, 190, 131, 80, 185, 114, 54,
    };
}