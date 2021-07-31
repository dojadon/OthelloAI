using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OthelloAI
{
    class WthorRecordReader
    {
        public delegate void OnLoadMoveEventhandler(Board board, int result);

        public event OnLoadMoveEventhandler OnLoadMove = delegate { };

        public delegate void OnLoadGameEventhandler(int gameCount);

        public event OnLoadGameEventhandler OnLoadGame = delegate { };

        public void Load(string path)
        {
            using var reader = new BinaryReader(new FileStream(path, FileMode.Open));

            byte[] data = reader.ReadBytes(4);

            int game_count = reader.ReadInt32();
            int record_count = reader.ReadInt16();
            int year = reader.ReadInt16();
            byte board_size = reader.ReadByte();
            byte type = reader.ReadByte();
            byte depth = reader.ReadByte();
            reader.ReadByte();

            for(int i = 0; i < game_count; i++)
            {
                reader.ReadBytes(6);
                byte stones = reader.ReadByte();
                byte stones_theoretical = reader.ReadByte();

                Board board = new Board(Board.InitB, Board.InitW);
                int stone = 1;

                for(int j = 0; j < 60; j++)
                {
                    int result = 60 - depth > j ? stones : stones_theoretical;
                    result = result * 2 - 64;

                    byte pos = reader.ReadByte();
                    int x = pos / 10 - 1;
                    int y = pos % 10 - 1;
                    //Console.WriteLine($"{pos}, {x}, {y}");
                    ulong move = Board.Mask(x, y);

                    if((board.GetMoves(stone) & move) != 0)
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
                    //board.print();
                    OnLoadMove(board, result);
                }
                OnLoadGame(i);
            }
        }
    }
}
