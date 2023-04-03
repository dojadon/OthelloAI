using OthelloAI.Condingame;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Text;

namespace OthelloAI
{
    public class Book
    {
        public int Randomness { get; set; } = 1;
        public Random Random { get; set; } = new Random();

        public Dictionary<Board, BookPosition> Positions { get; set; }
        public Dictionary<Board, int> Counts { get; set; } = new Dictionary<Board, int>();

        public static void Test()
        {
            using var reader = new BinaryReader(new FileStream("book.dat.store", FileMode.Open));

            var book = new Book();
            book.Read(reader);

            byte[] data = book.ToBytes(3200);
            string s = DataEncoding.Encode(data);
            File.WriteAllText("codingame/encoded_book.txt", s);
        }

        public Book()
        {
        }

        public Book(string path)
        {
            using var reader = new BinaryReader(new FileStream(path, FileMode.Open));
            Read(reader);
        }

        public ulong Search(Board board, int color)
        {
            if (color == -1)
                board = board.ColorFliped();

            var rotated = board;

            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    if (Positions.ContainsKey(rotated))
                    {
                        var position = Positions[rotated];

                        if (position.Links.Length == 0)
                            continue;

                        if (!Counts.ContainsKey(rotated))
                            Counts[rotated] = 0;

                        Counts[rotated]++;

                        var links = position.Links.OrderByDescending(l => l.eval).Take(Randomness);
                        var link = Random.Choice(links.ToArray());
                        ulong move = 1UL << link.move;

                        //foreach(var l in position.Links)
                        //{
                        //    Console.WriteLine(new Board(position.Board.bitB | (1UL << l.move), position.Board.bitW));
                        //    Console.WriteLine(l.eval);
                        //}

                        for (int k = 0; k < 4 - j; k++)
                            move = Board.Rotate90(move);

                        if (i == 1)
                            move = Board.HorizontalMirror(move);

                        return move;
                    }

                    rotated = rotated.Rotated90();
                }

                rotated = board.HorizontalMirrored();
            }

            return 0;
        }

        public void Read(BinaryReader reader)
        {
            byte[] header = reader.ReadBytes(8);

            byte version = reader.ReadByte();
            byte release = reader.ReadByte();

            byte[] data = reader.ReadBytes(7);

            reader.ReadByte();

            int level = reader.ReadInt32();
            int depth = 61 - reader.ReadInt32();

            int error1 = reader.ReadInt32();
            int error2 = reader.ReadInt32();

            int verbosity = reader.ReadInt32();
            int n = reader.ReadInt32();

            Console.WriteLine(level);
            Console.WriteLine(depth);
            Console.WriteLine(n);

            Positions = n.Loop(_ => ReadPosition(reader)).ToDictionary(p => p.Board);

            Console.WriteLine(Positions.Count(k => k.Key.n_stone < 18));
        }

        public BookPosition ReadPosition(BinaryReader reader)
        {
            ulong bit1 = reader.ReadUInt64();
            ulong bit2 = reader.ReadUInt64();
            var board = new Board(bit1, bit2);

            if(board.n_stone < 5)
            {
                Console.WriteLine(board.GetHashCode());
                Console.WriteLine(board);
            }

            if (board.GetHashCode() == -1587941263 || board.GetHashCode() == 1035302224)
            {
                Console.WriteLine(board.GetHashCode());
                Console.WriteLine(board);
            }

            int win = reader.ReadInt32();
            int draw = reader.ReadInt32();
            int lose = reader.ReadInt32();

            int n_lines = reader.ReadInt32();

            short eval = reader.ReadInt16();
            short e_min = reader.ReadInt16();
            short e_max = reader.ReadInt16();

            byte n_links = reader.ReadByte();
            byte level = reader.ReadByte();

            var links = new List<BookLink>();

            for (int i = 0; i < n_links; i++)
            {
                links.Add(new BookLink(reader.ReadSByte(), reader.ReadByte()));
            }

            var leaf = new BookLink(reader.ReadSByte(), reader.ReadByte());

            if (leaf.move < 65)
                links.Add(leaf);

            return new BookPosition(board, eval, links.ToArray());
        }

        public string ToBytesString(int n)
        {
            var builder = new StringBuilder();

            foreach(var t in Positions.OrderBy(t => t.Key.n_stone).Take(n))
            {
                builder.Append(t.Value.ToBytesString());
            }

            return builder.ToString();
        }

        public byte[] ToBytes(int n)
        {
            byte[] data = new byte[n * 5];
            var positions = Positions.OrderBy(t => t.Key.n_stone).Take(n).ToArray();

            for(int i = 0; i < positions.Length; i++)
            {
                positions[i].Value.Write(data, i * 5);
            }

            return data;
        }
    }

    public class BookPosition
    {
        public Board Board { get; init; }
        public int Eval { get; }

        public BookLink[] Links { get; }

        public BookPosition(Board board, int eval, BookLink[] links)
        {
            Board = board;
            Eval = eval;
            Links = links;
        }

        public string ToBytesString()
        {
            var link = Links.MaxBy(l => l.eval).First();
            return string.Format("{0:X8}{1:X2}", Board.GetHashCode(), link.move);
        }

        public void Write(byte[] dst, int offset)
        {
            var link = Links.MaxBy(l => l.eval).First();
            int index = Board.GetHashCode();

            dst[offset] = link.move;
            dst[offset + 1] = (byte)(index & 0xFF);
            dst[offset + 2] = (byte)((index >> 8) & 0xFF);
            dst[offset + 3] = (byte)((index >> 16) & 0xFF);
            dst[offset + 4] = (byte)((index >> 24) & 0xFF);
        }
    }

    public struct BookLink
    {
        public int eval;
        public byte move;

        public BookLink(int eval, byte move)
        {
            this.eval = eval;
            this.move = move;
        }
    }
}
