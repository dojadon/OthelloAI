using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

namespace OthelloAI.Condingame
{
    public static class Data
    {
        // 37 KB
        public const string BOOK_DATA = @"";

        // 25 KB
        public const string WEIGHT_DATA = @"";

        public const int DEPTH_1 = 7;
        public const int DEPTH_2 = 8;
        public const int ENDGAME = 50;

        public readonly static ulong[][] MASKS = new[] {
                new ulong[] { 0b11111111, 0b11000000_11100000_11100000, 0b00111100_00111100_00000000_00000000 },
                new ulong[] { 0b11111111, 0b11000000_11100000_11100000, 0b00111100_00111100_00000000_00000000 },
                new ulong[] { 0b11111111, 0b11000000_11100000_11100000, 0x8040201008040201UL },
                new ulong[] { 0b11111111, 0b11000000_11100000_11100000, 0x8040201008040201UL }
            };

        static void Test(string[] args)
        {
            WeightLight.Init();
            BookLight.InitBook();

            int id = int.Parse(Console.ReadLine()); // id of your player.
            int boardSize = int.Parse(Console.ReadLine());
            
            var timer = new Stopwatch();

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

                timer.Restart();

                B board = new B(b, w);

                (int x, int y, _) = PlayerLight.DecideMove(board, id == 0 ? 1 : -1); 

                timer.Stop();
                Console.Error.WriteLine("Taken Time : " + timer.ElapsedMilliseconds);
                Console.Error.WriteLine("Eval Time : " + WeightLight.t.Average());

                string row_labels = "abcdefgh";
                Console.WriteLine($"{row_labels[x]}{y + 1}");
            }
        }
    }

    public class BookLight
    {
        const int POSITION_SIZE = 10;

        public static readonly Dictionary<int, byte> Positions = new Dictionary<int, byte>();

        public static bool use_book = true;

        public static void InitBook()
        {
            int n = Data.BOOK_DATA.Length / POSITION_SIZE;

            for (int i = 0; i < n; i++)
            {
                string s = Data.BOOK_DATA[(POSITION_SIZE * i)..(POSITION_SIZE * (i + 1))];

                int hash = int.Parse(s[..8], System.Globalization.NumberStyles.HexNumber);
                byte move = byte.Parse(s[8..10], System.Globalization.NumberStyles.HexNumber);

                Positions[hash] = move;
            }
        }

        public static ulong SearchBook(B board)
        {
            if (!use_book)
                return 0;

            var rotated = board;

            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    int hash = rotated.GetHashCode();

                    if (Positions.ContainsKey(hash))
                    {
                        ulong move = 1UL << Positions[hash];

                        for (int k = 0; k < 4 - j; k++)
                            move = B.Rotate90(move);

                        if (i == 1)
                            move = B.HorizontalMirror(move);

                        return move;
                    }
                    rotated = rotated.Rotated90();
                }
                rotated = board.HorizontalMirrored();
            }
            return 0;
        }
    }

    public static class WeightLight
    {
        readonly static WeightTuple[] tuples = Data.MASKS.Select(m => new WeightTuple(m)).ToArray();
        static WeightTuple current;

        static Stopwatch timer = new Stopwatch();

        public static byte[] Decode(string encoded)
        {
            byte[] data = Encoding.UTF8.GetBytes(encoded);

            byte[] decoded = new byte[data.Length];

            for (int i = 0; i < data.Length / 3; i++)
            {
                ushort us = Decode(data, i * 3);

                decoded[i * 3] = (byte)((us & 0b11111_00000_00000) >> 10);
                decoded[i * 3 + 1] = (byte)((us & 0b11111_00000) >> 5);
                decoded[i * 3 + 2] = (byte)(us & 0b11111);
            }
            return decoded;
        }

        public static ushort Decode(byte[] src, int offset)
        {
            byte b1 = src[offset];
            byte b2 = src[offset + 1];
            byte b3 = src[offset + 2];

            ushort i = (ushort)(((b1 & 0xF) << 12) | ((b2 & 0b111111) << 6) | (b3 & 0b111111));

            if (i >= 0xB000)
                i -= 0x1000;

            i -= 0x4000;

            return i;
        }

        public static void Init()
        {
            byte[] data = Decode(Data.WEIGHT_DATA);
            EdaxWeightLight.Open(data);


        }

        public static void SetStage(int n_discs, int depth)
        {
            int stage = Math.Clamp((n_discs + depth + 5) / 10 - 2, 0, 3); ;
            current = tuples[stage];
        }

        public static List<float> t = new List<float>();

        public static int Eval(B b)
        {
            timer.Restart();

            int e = EdaxWeightLight.Eval(b);

            t.Add((float) timer.ElapsedTicks / Stopwatch.Frequency * 1000000000F);

            return e;
        }
    }

    public struct SP
    {
        public int depth;
        public float alpha, beta;

        public SP(int depth, float alpha, float beta)
        {
            this.depth = depth;
            this.alpha = alpha;
            this.beta = beta;
        }

        public static SP CreateInitParam(int depth)
        {
            return new SP(depth, -PlayerLight.INF, PlayerLight.INF);
        }

        public SP Deepen()
        {
            return new SP(depth - 1, -beta, -alpha);
        }

        public SP SwapAlphaBeta()
        {
            return new SP(depth, -beta, -alpha);
        }

        public SP CreateNullWindowParam()
        {
            return new SP(depth - 1, -alpha - 1, -alpha);
        }
    }

    public class PlayerLight
    {
        public const int INF = 10000000;

        public static Dictionary<B, (float, float)> table_prev;
        public static Dictionary<B, (float, float)> table = new Dictionary<B, (float, float)>();

        static MoveComparer comparer = new MoveComparer();

        public static bool use_transposition_cut = true;

        protected static float EvalFinishedGame(B board)
        {
            node_count++;

            return board.GetStoneCountGap() * 10000;
        }

        public static int node_count;

        public static float Eval(B board)
        {
            node_count++;

            if ((board.n_stone & 1) != 0)
            {
                return -WeightLight.Eval(board.ColorFliped());
            }
            else
            {
                return WeightLight.Eval(board);
            }
        }

        public static (int x, int y, ulong move) DecideMove(B board, int color)
        {
            if (color == -1)
                board = board.ColorFliped();

            ulong move = BookLight.SearchBook(board);

            if (move != 0)
            {
                Console.Error.WriteLine("Found Position");
                (int xx, int yy) = B.ToPos(move);
                return (xx, yy, move);
            }

            node_count = 0;

            ulong result;

            //if (board.n_stone == 18)
            //{
            //    (result, _) = SolveIterativeDeepening(board, SearchParameter.CreateInitParam(Data.DEPTH - 1), 2, 3);
            //}
            //else
            if (board.n_stone <= 40)
            {
                (result, _) = SolveIterativeDeepening(board, SP.CreateInitParam(Data.DEPTH_1), 2, 3);
            }
            else
            if (board.n_stone < Data.ENDGAME)
            {
                (result, _) = SolveIterativeDeepening(board, SP.CreateInitParam(Data.DEPTH_2), 2, 3);
            }
            else
            {
                (result, _) = SolveRoot(board, SP.CreateInitParam(64));
            }

            table = new Dictionary<B, (float, float)>();
            table_prev = null;

            (int x, int y) = B.ToPos(result);

            return (x, y, result);
        }

        public static (ulong, float) SolveIterativeDeepening(B board, SP p, int interval, int times)
        {
            WeightLight.SetStage(board.n_stone, p.depth);

            float depth = p.depth;
            p.depth -= interval * Math.Min(times - 1, (int)Math.Ceiling((double)p.depth / interval) - 1);

            while (true)
            {
                (ulong move, float e) = SolveRoot(board, p);

                if (p.depth >= depth)
                    return (move, e);

                table_prev = table;
                table = new Dictionary<B, (float, float)>();
                p.depth += interval;
            }
        }

        public static (ulong, float) SolveRoot(B board, SP p)
        {
            Move root = new Move(board);

            if (root.n_moves <= 1)
            {
                return (root.moves, 0);
            }

            Move[] array = root.NextMoves();
            if (p.depth >= 4 && table_prev != null)
                Array.Sort(array, comparer);
            else
                Array.Sort(array);

            Move result = array[0];
            float max = -Solve(array[0], p.Deepen());

            for (int i = 1; i < array.Length; i++)
            {
                Move move = array[i];
                float eval = -Solve(move, p.CreateNullWindowParam());

                if (p.alpha < eval)
                {
                    p.alpha = eval;
                    eval = -Solve(move, p.Deepen());
                    p.alpha = Math.Max(p.alpha, eval);
                }

                if (max < eval)
                {
                    max = eval;
                    result = move;
                }
            }
            return (result.move, max);
        }

        public static float NullWindowSearch(Move move, SP p)
        {
            return -Solve(move, p.CreateNullWindowParam());
        }

        public static float Negascout(B board, ulong moves, SP p)
        {
            ulong move = B.NextMove(moves);
            moves = B.RemoveMove(moves, move);
            float max = -Solve(new Move(board, move), p.Deepen());

            if (p.beta <= max)
                return max;

            p.alpha = Math.Max(p.alpha, max);

            while ((move = B.NextMove(moves)) != 0)
            {
                moves = B.RemoveMove(moves, move);
                Move m = new Move(board, move);

                float eval = NullWindowSearch(m, p);

                if (p.beta <= eval)
                    return eval;

                if (p.alpha < eval)
                {
                    p.alpha = eval;
                    eval = -Solve(m, p.Deepen());

                    if (p.beta <= eval)
                        return eval;

                    p.alpha = Math.Max(p.alpha, eval);
                }
                max = Math.Max(max, eval);
            }
            return max;
        }

        public static float Negascout(Move[] moves, SP p)
        {
            float max = -Solve(moves[0], p.Deepen());

            if (p.beta <= max)
                return max;

            p.alpha = Math.Max(p.alpha, max);

            foreach (Move move in moves.AsSpan(1, moves.Length - 1))
            {
                float eval = NullWindowSearch(move, p);

                if (p.beta <= eval)
                    return eval;

                if (p.alpha < eval)
                {
                    p.alpha = eval;
                    eval = -Solve(move, p.Deepen());

                    if (p.beta <= eval)
                        return eval;

                    p.alpha = Math.Max(p.alpha, eval);
                }
                max = Math.Max(max, eval);
            }
            return max;
        }

        public static float Negamax(Move[] moves, SP p)
        {
            float max = -1000000;

            for (int i = 0; i < moves.Length; i++)
            {
                float e = -Solve(moves[i], p.Deepen());
                max = Math.Max(max, e);
                p.alpha = Math.Max(p.alpha, e);

                if (p.alpha >= p.beta)
                    return max;
            }
            return max;
        }

        public static float Negamax(B board, ulong moves, SP p)
        {
            float max = -1000000;
            ulong move;
            while ((move = B.NextMove(moves)) != 0)
            {
                moves = B.RemoveMove(moves, move);

                float e = -Solve(new Move(board, move), p.Deepen());
                max = Math.Max(max, e);
                p.alpha = Math.Max(p.alpha, e);

                if (p.alpha >= p.beta)
                    return max;
            }
            return max;
        }

        public const int ordering_depth = 57;
        public const int transposition = 1;

        public static float Solve(Move move, SP p)
        {
            if (p.depth <= 0)
                return Eval(move.reversed);

            if (move.moves == 0)
            {
                ulong opponentMoves = move.reversed.GetOpponentMoves();
                if (opponentMoves == 0)
                {
                    return EvalFinishedGame(move.reversed);
                }
                else
                {
                    Move next = new Move(move.reversed.ColorFliped(), 0, opponentMoves, B.BitCount(opponentMoves));
                    return -Solve(next, p.SwapAlphaBeta());
                }
            }

            if (move.reversed.n_stone == 63 && move.n_moves == 1)
                return -EvalFinishedGame(move.reversed.Reversed(move.moves));

            float lower = -1000000;
            float upper = 1000000;

            if (p.depth > transposition && move.reversed.n_stone <= ordering_depth && use_transposition_cut && table.ContainsKey(move.reversed))
            {
                (lower, upper) = table[move.reversed];

                if (lower >= p.beta)
                {
                    return lower;
                }

                if (upper <= p.alpha || upper == lower)
                {
                    return upper;
                }

                p.alpha = Math.Max(p.alpha, lower);
                p.beta = Math.Min(p.beta, upper);
            }

            float value;

            if (p.depth >= 3 && move.reversed.n_stone < 60)
            {
                if (move.n_moves > 3)
                {
                    var moves = move.NextMoves();

                    if (p.depth >= 4 && table_prev != null)
                        Array.Sort(moves, comparer);
                    else
                        Array.Sort(moves);

                    value = Negascout(moves, p);
                }
                else
                    value = Negascout(move.reversed, move.moves, p);
            }
            else
            {
                value = Negamax(move.reversed, move.moves, p);
            }

            if (p.depth > transposition && move.reversed.n_stone <= ordering_depth && use_transposition_cut)
            {
                if (value <= p.alpha)
                    table[move.reversed] = (lower, value);
                else if (value >= p.beta)
                    table[move.reversed] = (value, upper);
                else
                    table[move.reversed] = (value, value);
            }

            return value;
        }

        class MoveComparer : IComparer<Move>
        {
            const int INTERVAL = 200;

            public static float Eval(Move move)
            {
                if (table_prev.TryGetValue(move.reversed, out (float min, float max) t))
                {
                    if (-INF < t.min && t.max < INF)
                        return (t.min + t.max) / 2;
                    else if (-INF < t.min)
                        return t.min / 2 + INTERVAL;
                    else if (INF > t.max)
                        return t.max / 2 - INTERVAL;
                }
                return INF + move.n_moves;
            }

            public int Compare([AllowNull] Move x, [AllowNull] Move y)
            {
                return Comparer<float>.Default.Compare(Eval(x), Eval(y));
            }
        }
    }

    public readonly struct Move : IComparable<Move>
    {
        public readonly ulong move;
        public readonly B reversed;
        public readonly ulong moves;
        public readonly int n_moves;

        public Move(B board, ulong move)
        {
            this.move = move;
            reversed = board.Reversed(move);
            moves = reversed.GetMoves();
            n_moves = B.BitCount(moves);
        }

        public Move(B reversed)
        {
            move = 0;
            this.reversed = reversed;
            moves = reversed.GetMoves();
            n_moves = B.BitCount(moves);
        }

        public Move(B reversed, ulong move, ulong moves, int count)
        {
            this.move = move;
            this.reversed = reversed;
            this.moves = moves;
            this.n_moves = count;
        }

        public Move[] NextMoves()
        {
            ulong moves_tmp = moves;

            Move[] array = new Move[n_moves];
            for (int i = 0; i < array.Length; i++)
            {
                ulong move = B.NextMove(moves_tmp);
                moves_tmp = B.RemoveMove(moves_tmp, move);
                array[i] = new Move(reversed, move);
            }
            return array;
        }

        public Move[] OrderedNextMoves()
        {
            Move[] moves = NextMoves();
            Array.Sort(moves);
            return moves;
        }

        public int CompareTo([AllowNull] Move other)
        {
            return n_moves - other.n_moves;
        }
    }

    public class RB
    {
        public readonly B rot0, rot90, rot180, rot270, inv_rot0, inv_rot90, inv_rot180, inv_rot270;

        public RB(B board)
        {
            rot0 = board;
            inv_rot0 = board.HorizontalMirrored();
            inv_rot90 = board.Transposed();
            inv_rot180 = board.VerticalMirrored();
            rot90 = inv_rot0.Transposed();
            rot180 = inv_rot180.HorizontalMirrored();
            rot270 = inv_rot90.HorizontalMirrored();
            inv_rot270 = rot270.VerticalMirrored();
        }
    }

    public readonly struct B
    {
        public const long InitB = 0x0000000810000000L;
        public const long InitW = 0x0000001008000000L;

        public static readonly B Init = new B(InitB, InitW);

        public readonly ulong bitB;
        public readonly ulong bitW;

        public readonly int n_stone;

        public B(B source)
        {
            bitB = source.bitB;
            bitW = source.bitW;
            n_stone = source.n_stone;
        }

        public B(ulong b, ulong w) : this(b, w, BitCount(b | w))
        {
        }

        public B(ulong b, ulong w, int count)
        {
            bitB = b;
            bitW = w;
            n_stone = count;
        }

        public B HorizontalMirrored() => new B(HorizontalMirror(bitB), HorizontalMirror(bitW), n_stone);

        public static ulong HorizontalMirror(ulong x)
        {
            return BinaryPrimitives.ReverseEndianness(x);
        }

        public B VerticalMirrored() => new B(VerticalMirror(bitB), VerticalMirror(bitW), n_stone);

        public static ulong VerticalMirror(ulong b)
        {
            b = ((b >> 1) & 0x5555555555555555UL) | ((b << 1) & 0xAAAAAAAAAAAAAAAAUL);
            b = ((b >> 2) & 0x3333333333333333UL) | ((b << 2) & 0xCCCCCCCCCCCCCCCCUL);
            b = ((b >> 4) & 0x0F0F0F0F0F0F0F0FUL) | ((b << 4) & 0xF0F0F0F0F0F0F0F0UL);

            return b;
        }

        public B Transposed() => new B(Transpose(bitB), Transpose(bitW), n_stone);

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

        public static ulong Rotate90(ulong x) => Transpose(HorizontalMirror(x));

        public B Rotated90() => new B(Rotate90(bitB), Rotate90(bitW), n_stone);

        public static int BitCount(ulong v)
        {
            return BitOperations.PopCount(v);
        }

        public static ulong LowestOneBit(ulong i)
        {
            return i & (~i + 1);
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

        public static (int, int) ToPos(ulong move)
        {
            int x = BitOperations.TrailingZeroCount(move);
            return (x / 8, x & 7);
        }

        public override int GetHashCode()
        {
            int result = 0;
            result = result * 31 + (int)(bitB ^ (bitB >> 32));
            result = result * 31 + (int)(bitW ^ (bitW >> 32));
            return result;
        }

        public override bool Equals(object obj)
        {
            return (obj is B b) && (b.bitB == bitB) && (b.bitW == bitW);
        }

        public ulong GetMoves() => GetMovesAvx2(bitB, bitW);

        public ulong GetMoves(int stone) => stone switch
        {
            1 => GetMovesAvx2(bitB, bitW),
            -1 => GetMovesAvx2(bitW, bitB),
            _ => 0,
        };

        public ulong GetOpponentMoves() => GetMovesAvx2(bitW, bitB);

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

        public B ColorFliped()
        {
            return new B(bitW, bitB, n_stone);
        }

        public B Reversed(ulong move)
        {
            ulong reversed = ReverseAvx(move, bitB, bitW);
            return new B(bitW ^ reversed, bitB ^ (move | reversed), n_stone + 1);
        }

        public B Reversed(ulong move, int stone)
        {
            ulong reversed;

            switch (stone)
            {
                case 1:
                    reversed = ReverseAvx(move, bitB, bitW);
                    return new B(bitB ^ (move | reversed), bitW ^ reversed, n_stone + 1);

                case -1:
                    reversed = ReverseAvx(move, bitW, bitB);
                    return new B(bitB ^ reversed, bitW ^ (move | reversed), n_stone + 1);
            }
            return this;
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

        public int GetStoneCountGap()
        {
            return (2 * BitCount(bitB) - n_stone);
        }

        public int GetStoneCountGap(int s)
        {
            return s * GetStoneCountGap();
        }

        public static ulong NextMove(ulong moves)
        {
            return LowestOneBit(moves);
        }

        public static ulong RemoveMove(ulong moves, ulong move)
        {
            return moves ^ move;
        }

        public static bool operator ==(B b1, B b2) => (b1.bitB == b2.bitB) && (b1.bitW == b2.bitW);

        public static bool operator !=(B b1, B b2) => (b1.bitB != b2.bitB) || (b1.bitW != b2.bitW);

        public override string ToString()
        {
            B b = this;
            string Disc(int x, int y) => b.GetId(x, y) switch
            {
                0 => " ",
                1 => "X",
                2 => "O",
                _ => "?"
            };

            string Line(int y)
            {
                return $"{y} | {Disc(0, y)} | {Disc(1, y)} | {Disc(2, y)} | {Disc(3, y)} | {Disc(4, y)} | {Disc(5, y)} | {Disc(6, y)} | {Disc(7, y)} |";
            }

            return string.Join(Environment.NewLine,
                $"    0   1   2   3   4   5   6   7",
                $"  +---+---+---+---+---+---+---+---+", Line(0),
                $"  +---+---+---+---+---+---+---+---+", Line(1),
                $"  +---+---+---+---+---+---+---+---+", Line(2),
                $"  +---+---+---+---+---+---+---+---+", Line(3),
                $"  +---+---+---+---+---+---+---+---+", Line(4),
                $"  +---+---+---+---+---+---+---+---+", Line(5),
                $"  +---+---+---+---+---+---+---+---+", Line(6),
                $"  +---+---+---+---+---+---+---+---+", Line(7),
                $"  +---+---+---+---+---+---+---+---+");
        }
    }
}
