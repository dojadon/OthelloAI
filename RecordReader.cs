using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OthelloAI
{
    interface IGameProvider
    {
        IEnumerable<TrainingData> Read();
    }

    class GameProviderSelfMatching : IGameProvider
    {
        PlayerAI Player { get; }
        int NumGames { get; }

        public IEnumerable<TrainingData> Read()
        {
            for(int i = 0; i < NumGames; i++)
            {
                yield return CreateGame();
            }
        }

        public TrainingData CreateGame()
        {
            (var _, var boards) = Tester.PlayGame(Player, Player, Board.Init, r => r.next_board);

            var data = new TrainingData
            {
                { boards, boards[^1].GetStoneCountGap() }
            };
            return data;
        }
    }

    class WthorRecordReader : IGameProvider
    {
        public string Path { get; }

        public WthorRecordReader(string path)
        {
            Path = path;
        }

        public static IEnumerable<TrainingData> Read(string path)
        {
            return new WthorRecordReader(path).Read();
        }

        public IEnumerable<TrainingData> Read()
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
                int result = reader.ReadByte() * 2 - 64;

                var boards = new List<Board>();
                Board board = new Board(Board.InitB, Board.InitW);
                int color = 1;

                for (int j = 0; j < 60; j++)
                {
                    byte pos = reader.ReadByte();
                    int x = pos / 10 - 1;
                    int y = pos % 10 - 1;
                    //Console.WriteLine($"{pos}, {x}, {y}");
                    ulong move = Board.Mask(x, y);

                    if ((board.GetMoves(color) & move) != 0)
                    {
                        board = board.Reversed(move, color);
                        boards.Add(board);
                        color = -color;
                    }
                    else if ((board.GetMoves(-color) & move) != 0)
                    {
                        board = board.Reversed(move, -color);
                        boards.Add(board);
                    }
                    else
                    {
                        continue;
                    }
                }

                yield return new TrainingData(boards.Select(b => new TrainingDataElement(b, result)));
            }
        }
    }
}
