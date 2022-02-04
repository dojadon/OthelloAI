using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OthelloAI
{
    abstract class RecordReader
    {
        public delegate void OnLoadMoveEventhandler(Board board, int result);

        public abstract event OnLoadMoveEventhandler OnLoadMove;

        public delegate void OnLoadGameEventhandler(int gameCount);

        public abstract event OnLoadGameEventhandler OnLoadGame;

        public abstract void Read();
    }

    class MyRecordReader : RecordReader
    {
        public override event OnLoadMoveEventhandler OnLoadMove = delegate { };
        public override event OnLoadGameEventhandler OnLoadGame = delegate { };

        public string Path { get; set; }

        public MyRecordReader(string path)
        {
            Path = path;
        }

        public static int Reverse(int value) => (int)Reverse((uint)value);

        public static uint Reverse(uint value)
        {
            return (value & 0xFF) << 24 |
                    ((value >> 8) & 0xFF) << 16 |
                    ((value >> 16) & 0xFF) << 8 |
                    ((value >> 24) & 0xFF);
        }

        public override void Read()
        {
            using var reader = new BinaryReader(new FileStream(Path, FileMode.Open));
            int gameCount = 0;

            while (reader.BaseStream.Position != reader.BaseStream.Length)
            {
                int count = Reverse(reader.ReadInt32()) / 2;
                Board[] boards = new Board[count];

                for (int i = 0; i < boards.Length; i++)
                {
                    boards[i] = new Board(reader.ReadUInt64(), reader.ReadUInt64());
                }

                int result = boards[^1].GetStoneCountGap();
                foreach (var board in boards)
                {
                    OnLoadMove(board, result);
                }

                OnLoadGame(gameCount++);
            }
        }
    }

    class WthorRecordReader : RecordReader
    {
        public override event OnLoadMoveEventhandler OnLoadMove = delegate { };
        public override event OnLoadGameEventhandler OnLoadGame = delegate { };

        public string Path { get; set; }

        public WthorRecordReader(string path)
        {
            Path = path;
        }

        public override void Read()
        {
            using var reader = new BinaryReader(new FileStream(Path, FileMode.Open));

            byte[] data = reader.ReadBytes(4);

            int game_count = reader.ReadInt32();
            int record_count = reader.ReadInt16();
            int year = reader.ReadInt16();
            byte board_size = reader.ReadByte();
            byte type = reader.ReadByte();
            byte depth = reader.ReadByte();
            reader.ReadByte();

            for (int i = 0; i < game_count; i++)
            {
                reader.ReadBytes(6);
                byte stones = reader.ReadByte();
                byte stones_theoretical = reader.ReadByte();

                Board board = new Board(Board.InitB, Board.InitW);
                int stone = 1;

                for (int j = 0; j < 60; j++)
                {
                    int result = 60 - depth > j ? stones : stones_theoretical;
                    result = result * 2 - 64;

                    byte pos = reader.ReadByte();
                    int x = pos / 10 - 1;
                    int y = pos % 10 - 1;
                    //Console.WriteLine($"{pos}, {x}, {y}");
                    ulong move = Board.Mask(x, y);

                    if ((board.GetMoves(stone) & move) != 0)
                    {
                        board = board.Reversed(move, stone);
                        stone = -stone;
                    }
                    else if ((board.GetMoves(-stone) & move) != 0)
                    {
                        board = board.Reversed(move, -stone);
                    }
                    else
                    {
                        break;
                    }
                    OnLoadMove(board, result);
                }
                OnLoadGame(i);
            }
        }
    }
}
